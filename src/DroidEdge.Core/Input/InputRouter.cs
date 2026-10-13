using DroidEdge.Core.Config;
using DroidEdge.Core.Hid;

namespace DroidEdge.Core.Input;

/// <summary>Decides whether input belongs to the PC or the phone and translates it into HID reports.</summary>
public sealed class InputRouter
{
    private const int WheelDelta = 120;
    private const int DragAtEdgeDwellMs = 300;

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
    private readonly DwellTrigger _dragAtEdge = new(DragAtEdgeDwellMs);
    private readonly KeyboardState _keyboard = new();
    private PointerTracker? _phone;
    private HidMouseButtons _buttons;
    // Buttons held on the PC. Crossing while one is held would swallow its release and leave Windows thinking it is still down.
    private HidMouseButtons _pcButtons;
    private bool _dragAtEdgeRaised;
    // While the phone desktop keeps the phone awake, its own screen is only lit while the mouse is on it.
    private bool _phoneKeptAwake;
    private bool _screenOffByUs;
    private int _wheelAcc, _panAcc;
    private int _cursorX, _cursorY;
    private readonly Queue<bool> _pendingModeChanges = new();
    private bool _draining;

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
    /// <summary>A mouse button is held while pushing against the edge (e.g. dragging files toward the phone); args are the cursor position.</summary>
    public event Action<int, int>? DragAtEdge;

    /// <param name="pixelsPerCount">How far the phone moves its cursor per HID count (see <see cref="PointerTracker.PixelsPerCountFor"/>).</param>
    public void SetPhoneReady(int width, int height, double pixelsPerCount)
    {
        lock (_lock)
        {
            _phone = new PointerTracker(width, height, _cfg.Sensitivity, pixelsPerCount);
            PhoneReady = true;
            _enter.Reset();
            _log($"Phone ready: {width}x{height}, {pixelsPerCount:0.###} px per count");
            _screenOffByUs = false; // a new server; the previous one turned the screen back on when it exited
            if (_phoneKeptAwake && !OnPhone) TurnScreenOff();
        }
    }

    public void SetPhoneUnavailable()
    {
        lock (_lock)
        {
            ReturnToPcCore(sendReleases: false);
            PhoneReady = false;
            _phone = null;
            _screenOffByUs = false;
        }
        FlushModeChanged();
    }

    /// <summary>The phone desktop window started or stopped keeping the phone awake (it opened or closed).</summary>
    public void SetPhoneKeptAwake(bool keptAwake)
    {
        lock (_lock)
        {
            _phoneKeptAwake = keptAwake;
            if (keptAwake && PhoneReady && !OnPhone) TurnScreenOff();
        }
    }

    public void UpdatePhoneBounds(int width, int height, double pixelsPerCount)
    {
        lock (_lock)
        {
            if (_phone is null) return;
            _phone.SetBounds(width, height, pixelsPerCount);
            _log($"Phone bounds updated: {width}x{height}, {pixelsPerCount:0.###} px per count");
            if (OnPhone)
            {
                foreach (var (cx, cy) in _phone.Resync())
                    _sink.SendMouse(MouseReport.Encode(_buttons, cx, cy, 0, 0));
                _log($"Resynced phone cursor to ({_phone.X},{_phone.Y}) after bounds change");
            }
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
        (int x, int y)? dragAt;
        lock (_lock)
        {
            dragAt = OnRawMouseMoveLocked(dx, dy);
        }
        FlushModeChanged();
        if (dragAt is var (x, y))
        {
            try { DragAtEdge?.Invoke(x, y); }
            catch (Exception ex) { _log($"DragAtEdge subscriber threw: {ex}"); }
        }
    }

    // Caller holds _lock. Returns the cursor position when a drag at the edge should be reported.
    private (int x, int y)? OnRawMouseMoveLocked(int dx, int dy)
    {
        if (!OnPhone)
        {
            if (!PhoneReady || _phone is null) return null;
            bool pushing = EdgeGeometry.IsPushingOut(_cfg.Edge, _segment, _cursorX, _cursorY, dx, dy);
            bool held = _pcButtons != HidMouseButtons.None;
            if (_enter.Sample(pushing && !held, _clock.NowMs)) EnterPhone();
            if (_dragAtEdge.Sample(pushing && held, _clock.NowMs) && !_dragAtEdgeRaised)
            {
                _dragAtEdgeRaised = true;
                return (_cursorX, _cursorY);
            }
            return null;
        }

        var (countX, countY) = _phone!.Move(dx, dy);
        bool back = EdgeGeometry.IsPushingBack(_cfg.Edge, _phone, dx, dy);
        if (_exit.Sample(back, _clock.NowMs))
        {
            ReturnToPcCore(sendReleases: true);
            return null;
        }
        if (countX != 0 || countY != 0)
        {
            foreach (var (cx, cy) in MoveChunker.Chunk(countX, countY))
                _sink.SendMouse(MouseReport.Encode(_buttons, cx, cy, 0, 0));
        }
        return null;
    }

    public void OnRawMouseButton(HidMouseButtons button, bool down)
    {
        lock (_lock) OnRawMouseButtonLocked(button, down);
        FlushModeChanged(); // the middle-click "done" path can return to the PC
    }

    // Caller holds _lock.
    private void OnRawMouseButtonLocked(HidMouseButtons button, bool down)
    {
        if (!OnPhone)
        {
            _pcButtons = down ? _pcButtons | button : _pcButtons & ~button;
            if (_pcButtons == HidMouseButtons.None)
            {
                _dragAtEdgeRaised = false;
                _dragAtEdge.Reset();
            }
            return;
        }
        if (button == HidMouseButtons.Right && _cfg.RightClickIsBack)
        {
            // Android treats a secondary mouse button as a context click, not Back, so the server injects KEYCODE_BACK instead.
            _sink.SendBack(down);
            return;
        }
        if (button == HidMouseButtons.Middle && _cfg.MiddleClickIsDone)
        {
            // "I'm done": turn the phone screen off and hand the mouse back to the PC.
            if (!down) return;
            _log("Middle click: turning the phone screen off and returning to PC");
            TurnScreenOff();
            ReturnToPcCore(sendReleases: true);
            return;
        }
        var next = down ? _buttons | button : _buttons & ~button;
        if (next == _buttons) return;
        _buttons = next;
        _sink.SendMouse(MouseReport.Encode(_buttons, 0, 0, 0, 0));
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
        TurnScreenOn();
        double fraction = EdgeGeometry.Fraction(_cfg.Edge, _monitor, _cursorX, _cursorY);
        var (px, py) = EdgeGeometry.PointOnEdge(_cfg.Edge, _monitor, fraction, inset: 1);
        _cursor.SetPosition(px, py);
        var (tx, ty) = EdgeGeometry.PhoneEntry(_cfg.Edge, fraction, _phone.Width, _phone.Height);
        _phone.SetPosition(tx, ty);
        _buttons = HidMouseButtons.None;
        _wheelAcc = _panAcc = 0;
        _keyboard.ReleaseAll();
        _exit.Reset();
        foreach (var (cx, cy) in _phone.Resync())
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
            if (_phoneKeptAwake) TurnScreenOff();
        }
        _buttons = HidMouseButtons.None;
        _keyboard.ReleaseAll();
        _log("Returned to PC");
        _pendingModeChanges.Enqueue(false);
    }

    // Caller holds _lock.
    private void TurnScreenOff()
    {
        if (_screenOffByUs) return;
        _sink.SendDisplayPower(false);
        _screenOffByUs = true;
    }

    // Caller holds _lock. Only turns on a screen we turned off, so a phone that is asleep or locked is left alone.
    private void TurnScreenOn()
    {
        if (!_screenOffByUs) return;
        _sink.SendDisplayPower(true);
        _screenOffByUs = false;
    }

    // Called after the lock is released so subscribers can block or call back into the router safely.
    // Exactly one thread drains at a time, so changes are delivered in the order they were queued.
    private void FlushModeChanged()
    {
        lock (_lock)
        {
            if (_draining) return; // the active drainer will deliver anything queued after this point
            _draining = true;
        }
        try
        {
            while (true)
            {
                bool change;
                lock (_lock)
                {
                    if (_pendingModeChanges.Count == 0)
                    {
                        _draining = false; // released under the same lock as the emptiness check, so no item can be stranded
                        return;
                    }
                    change = _pendingModeChanges.Dequeue();
                }
                try { ModeChanged?.Invoke(change); }
                catch (Exception ex) { _log($"ModeChanged subscriber threw: {ex}"); }
            }
        }
        catch
        {
            lock (_lock) _draining = false;
            throw;
        }
    }
}
