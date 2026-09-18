using PhoneEdge.Core.Config;
using PhoneEdge.Core.Hid;

namespace PhoneEdge.Core.Input;

/// <summary>Decides whether input belongs to the PC or the phone and translates it into HID reports.</summary>
public sealed class InputRouter
{
    private const int WheelDelta = 120;

    private readonly AppConfig _cfg;
    private readonly IntRect _monitor;
    private readonly IntRect _segment;
    private readonly ICursor _cursor;
    private readonly IInputGate _gate;
    private readonly IReportSink _sink;
    private readonly IClock _clock;
    private readonly Action<string> _log;
    private readonly object _lock = new();

    private readonly DwellTrigger _enter;
    private readonly DwellTrigger _exit;
    private readonly KeyboardState _keyboard = new();
    private PointerTracker? _phone;
    private HidMouseButtons _buttons;
    private int _wheelAcc, _panAcc;
    private int _cursorX, _cursorY;
    private readonly Queue<bool> _pendingModeChanges = new();

    public InputRouter(AppConfig cfg, IntRect monitor, IntRect edgeSegment, ICursor cursor, IInputGate gate, IReportSink sink, IClock clock, Action<string>? log = null)
    {
        _cfg = cfg;
        _monitor = monitor;
        _segment = edgeSegment;
        _cursor = cursor;
        _gate = gate;
        _sink = sink;
        _clock = clock;
        _log = log ?? (_ => { });
        _enter = new DwellTrigger(cfg.EnterDwellMs);
        _exit = new DwellTrigger(cfg.ExitDwellMs);
        (_cursorX, _cursorY) = cursor.GetPosition();
    }

    public bool OnPhone { get; private set; }
    public bool PhoneReady { get; private set; }
    public event Action<bool>? ModeChanged;

    public void SetPhoneReady(int width, int height)
    {
        lock (_lock)
        {
            _phone = new PointerTracker(width, height, _cfg.Sensitivity);
            PhoneReady = true;
            _enter.Reset();
            _log($"Phone ready: {width}x{height}");
        }
    }

    public void SetPhoneUnavailable()
    {
        lock (_lock)
        {
            ReturnToPcCore(sendReleases: false);
            PhoneReady = false;
            _phone = null;
        }
        FlushModeChanged();
    }

    public void UpdatePhoneBounds(int width, int height)
    {
        lock (_lock)
        {
            if (_phone is null) return;
            _phone.SetBounds(width, height);
            _log($"Phone bounds updated: {width}x{height}");
        }
    }

    public void OnCursorMoved(int x, int y)
    {
        lock (_lock)
        {
            _cursorX = x;
            _cursorY = y;
        }
    }

    public void OnRawMouseMove(int dx, int dy)
    {
        lock (_lock)
        {
            OnRawMouseMoveLocked(dx, dy);
        }
        FlushModeChanged();
    }

    // Caller holds _lock.
    private void OnRawMouseMoveLocked(int dx, int dy)
    {
        if (!OnPhone)
        {
            if (!PhoneReady || _phone is null) return;
            bool pushing = EdgeGeometry.IsPushingOut(_cfg.Edge, _segment, _cursorX, _cursorY, dx, dy);
            if (_enter.Sample(pushing, _clock.NowMs)) EnterPhone();
            return;
        }

        var (adx, ady) = _phone!.Move(dx, dy);
        bool back = EdgeGeometry.IsPushingBack(_cfg.Edge, _phone, dx, dy);
        if (_exit.Sample(back, _clock.NowMs))
        {
            ReturnToPcCore(sendReleases: true);
            return;
        }
        if (adx != 0 || ady != 0)
        {
            foreach (var (cx, cy) in MoveChunker.Chunk(adx, ady))
                _sink.SendMouse(MouseReport.Encode(_buttons, cx, cy, 0, 0));
        }
    }

    public void OnRawMouseButton(HidMouseButtons button, bool down)
    {
        lock (_lock)
        {
            if (!OnPhone) return;
            var next = down ? _buttons | button : _buttons & ~button;
            if (next == _buttons) return;
            _buttons = next;
            _sink.SendMouse(MouseReport.Encode(_buttons, 0, 0, 0, 0));
        }
    }

    public void OnRawWheel(int delta, bool horizontal)
    {
        lock (_lock)
        {
            if (!OnPhone) return;
            ref int acc = ref (horizontal ? ref _panAcc : ref _wheelAcc);
            acc += delta;
            int ticks = acc / WheelDelta;
            if (ticks == 0) return;
            acc -= ticks * WheelDelta;
            sbyte t = (sbyte)Math.Clamp(ticks, -127, 127);
            _sink.SendMouse(horizontal
                ? MouseReport.Encode(_buttons, 0, 0, 0, t)
                : MouseReport.Encode(_buttons, 0, 0, t, 0));
        }
    }

    public void OnRawKey(byte hidUsage, bool down)
    {
        lock (_lock)
        {
            if (!OnPhone || !_cfg.ForwardKeyboard) return;
            bool changed = down ? _keyboard.Press(hidUsage) : _keyboard.Release(hidUsage);
            if (changed) _sink.SendKeyboard(_keyboard.ToReport());
        }
    }

    public void ReturnToPc()
    {
        lock (_lock) ReturnToPcCore(sendReleases: true);
        FlushModeChanged();
    }

    // Caller holds _lock.
    private void EnterPhone()
    {
        if (_phone is null) return;
        OnPhone = true;
        _gate.Swallow = true;
        double fraction = EdgeGeometry.Fraction(_cfg.Edge, _monitor, _cursorX, _cursorY);
        var (px, py) = EdgeGeometry.PointOnEdge(_cfg.Edge, _monitor, fraction, inset: 1);
        _cursor.SetPosition(px, py);
        var (tx, ty) = EdgeGeometry.PhoneEntry(_cfg.Edge, fraction, _phone.Width, _phone.Height);
        _phone.SetPosition(tx, ty);
        _buttons = HidMouseButtons.None;
        _wheelAcc = _panAcc = 0;
        _keyboard.ReleaseAll();
        _exit.Reset();
        foreach (var (cx, cy) in MoveChunker.Resync(_phone.Width, _phone.Height, tx, ty))
            _sink.SendMouse(MouseReport.Encode(HidMouseButtons.None, cx, cy, 0, 0));
        _log($"Entered phone at ({tx},{ty}) from PC ({_cursorX},{_cursorY})");
        _pendingModeChanges.Enqueue(true);
    }

    // Caller holds _lock. Idempotent.
    private void ReturnToPcCore(bool sendReleases)
    {
        _enter.Reset();
        _exit.Reset();
        if (!OnPhone) return;
        OnPhone = false;
        _gate.Swallow = false;
        if (_phone is not null)
        {
            double fraction = EdgeGeometry.PhoneFraction(_cfg.Edge, _phone);
            var (px, py) = EdgeGeometry.PointOnEdge(_cfg.Edge, _monitor, fraction, inset: 1);
            _cursor.SetPosition(px, py);
            (_cursorX, _cursorY) = (px, py);
        }
        if (sendReleases)
        {
            if (_buttons != HidMouseButtons.None) _sink.SendMouse(MouseReport.Encode(HidMouseButtons.None, 0, 0, 0, 0));
            if (_cfg.ForwardKeyboard) _sink.SendKeyboard(KeyboardState.EmptyReport);
        }
        _buttons = HidMouseButtons.None;
        _keyboard.ReleaseAll();
        _log("Returned to PC");
        _pendingModeChanges.Enqueue(false);
    }

    // Called after the lock is released so subscribers can block or call back into the router safely.
    // Drains every pending change in order so racing transitions are never lost or reordered.
    private void FlushModeChanged()
    {
        while (true)
        {
            bool change;
            lock (_lock)
            {
                if (_pendingModeChanges.Count == 0) return;
                change = _pendingModeChanges.Dequeue();
            }
            ModeChanged?.Invoke(change);
        }
    }
}
