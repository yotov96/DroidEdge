using PhoneEdge.Core;
using PhoneEdge.Core.Config;
using PhoneEdge.Core.Hid;
using PhoneEdge.Core.Input;

namespace PhoneEdge.Tests;

public class InputRouterTests
{
    private sealed class FakeSink : IReportSink
    {
        public List<byte[]> Mouse { get; } = new();
        public List<byte[]> Keyboard { get; } = new();
        public List<bool> Back { get; } = new();
        public bool SendMouse(byte[] r) { Mouse.Add(r); return true; }
        public bool SendKeyboard(byte[] r) { Keyboard.Add(r); return true; }
        public bool SendBack(bool down) { Back.Add(down); return true; }
    }
    private sealed class FakeCursor : ICursor
    {
        public (int x, int y) Pos = (500, 500);
        public (int x, int y) GetPosition() => Pos;
        public void SetPosition(int x, int y) => Pos = (x, y);
    }
    private sealed class FakeGate : IInputGate { public bool Swallow { get; set; } }
    private sealed class FakeClock : IClock { public long NowMs { get; set; } }

    private static readonly IntRect Mon = new(0, 0, 1920, 1080);
    private static readonly IntRect Seg = new(0, 0, 1, 1080);
    private const int W = 1344, H = 2992;

    private (InputRouter r, FakeSink s, FakeCursor c, FakeGate g, FakeClock k) Make(AppConfig? cfg = null)
    {
        cfg ??= AppConfig.Default with { EnterDwellMs = 1000, ExitDwellMs = 500 };
        var s = new FakeSink(); var c = new FakeCursor(); var g = new FakeGate(); var k = new FakeClock();
        var r = new InputRouter(cfg, Mon, Seg, c, g, s, k);
        return (r, s, c, g, k);
    }

    /// <summary>Push left at the edge until the dwell completes (stops as soon as the router enters phone mode).</summary>
    private static void PushIn(InputRouter r, FakeClock k, int y = 540)
    {
        r.OnCursorMoved(0, y);
        for (int t = 0; t <= 1100; t += 50)
        {
            k.NowMs = t;
            r.OnRawMouseMove(-2, 0);
            if (r.OnPhone) break;
        }
    }

    [Fact]
    public void Does_not_enter_when_phone_not_ready()
    {
        var (r, _, _, g, k) = Make();
        PushIn(r, k);
        Assert.False(r.OnPhone);
        Assert.False(g.Swallow);
    }

    [Fact]
    public void Enters_when_hook_reports_cursor_beyond_the_edge()
    {
        // Real WH_MOUSE_LL positions while pushing against the left edge are negative (unclipped).
        var (r, _, c, g, k) = Make();
        r.SetPhoneReady(W, H);
        for (int t = 0; t <= 1100 && !r.OnPhone; t += 50)
        {
            r.OnCursorMoved(-(1 + t / 50), 540);
            k.NowMs = t;
            r.OnRawMouseMove(-4, 0);
        }
        Assert.True(r.OnPhone);
        Assert.True(g.Swallow);
        Assert.Equal((1, 540), c.Pos);
    }

    [Fact]
    public void Enters_after_dwell_parks_cursor_and_resyncs_phone_cursor()
    {
        var (r, s, c, g, k) = Make();
        bool? mode = null; r.ModeChanged += m => mode = m;
        r.SetPhoneReady(W, H);
        PushIn(r, k, y: 540);
        Assert.True(r.OnPhone);
        Assert.True(g.Swallow);
        Assert.Equal((1, 540), c.Pos);
        Assert.True(mode);
        // Resync: sum of all moves = -(W+127) -(H+127) + target (W-1, ~H/2)
        int sx = s.Mouse.Sum(m => (int)(sbyte)m[1]);
        int sy = s.Mouse.Sum(m => (int)(sbyte)m[2]);
        Assert.Equal(-(W + 127) + (W - 1), sx);
        int expectedY = (int)Math.Round(540.0 / 1079 * (H - 1)); // fraction along the 1080-high edge mapped onto the phone height
        Assert.Equal(-(H + 127) + expectedY, sy);
    }

    [Fact]
    public void Pushing_with_gap_does_not_enter()
    {
        var (r, _, _, _, k) = Make();
        r.SetPhoneReady(W, H);
        r.OnCursorMoved(0, 540);
        k.NowMs = 0; r.OnRawMouseMove(-1, 0);
        k.NowMs = 600; r.OnRawMouseMove(-1, 0);   // gap > 150 → restart
        k.NowMs = 1200; r.OnRawMouseMove(-1, 0);  // gap again
        Assert.False(r.OnPhone);
    }

    [Fact]
    public void On_phone_moves_are_forwarded_and_clamped()
    {
        var (r, s, _, _, k) = Make();
        r.SetPhoneReady(W, H);
        PushIn(r, k);
        s.Mouse.Clear();
        r.OnRawMouseMove(-10, 3);
        Assert.Single(s.Mouse);
        Assert.Equal(unchecked((byte)-10), s.Mouse[0][1]);
        Assert.Equal(3, s.Mouse[0][2]);
        s.Mouse.Clear();
        r.OnRawMouseMove(5, 0); // back toward the right edge: 10 px room → applied 5
        Assert.Equal(5, (sbyte)s.Mouse[0][1]);
        s.Mouse.Clear();
        r.OnRawMouseMove(50, 0); // only 5 px room → applied 5, then clamped
        Assert.Equal(5, (sbyte)s.Mouse[0][1]);
        s.Mouse.Clear();
        k.NowMs += 1; r.OnRawMouseMove(50, 0); // clamped: nothing applied, nothing sent
        Assert.Empty(s.Mouse);
    }

    [Fact]
    public void Buttons_and_wheel_are_forwarded()
    {
        var (r, s, _, _, k) = Make();
        r.SetPhoneReady(W, H);
        PushIn(r, k);
        s.Mouse.Clear();
        r.OnRawMouseButton(HidMouseButtons.Left, true);
        Assert.Equal(1, s.Mouse[^1][0]);
        r.OnRawMouseMove(-1, 0);
        Assert.Equal(1, s.Mouse[^1][0]); // button held during move
        r.OnRawMouseButton(HidMouseButtons.Left, false);
        Assert.Equal(0, s.Mouse[^1][0]);
        s.Mouse.Clear();
        r.OnRawWheel(60, false);
        Assert.Empty(s.Mouse); // half a notch accumulates
        r.OnRawWheel(60, false);
        Assert.Equal(1, (sbyte)s.Mouse[^1][3]);
        r.OnRawWheel(-240, true);
        Assert.Equal(-2, (sbyte)s.Mouse[^1][4]);
    }

    [Fact]
    public void Right_click_sends_back_instead_of_a_secondary_button()
    {
        var (r, s, _, _, k) = Make();
        r.SetPhoneReady(W, H);
        PushIn(r, k);
        s.Mouse.Clear();
        r.OnRawMouseButton(HidMouseButtons.Right, true);
        r.OnRawMouseButton(HidMouseButtons.Right, false);
        Assert.Equal(new[] { true, false }, s.Back);
        Assert.Empty(s.Mouse); // no HID button report at all
    }

    [Fact]
    public void Right_click_is_a_secondary_button_when_the_option_is_off()
    {
        var (r, s, _, _, k) = Make(AppConfig.Default with { EnterDwellMs = 1000, RightClickIsBack = false });
        r.SetPhoneReady(W, H);
        PushIn(r, k);
        s.Mouse.Clear();
        r.OnRawMouseButton(HidMouseButtons.Right, true);
        Assert.Empty(s.Back);
        Assert.Equal((byte)HidMouseButtons.Right, s.Mouse[^1][0]);
    }

    [Fact]
    public void Right_click_on_the_pc_is_ignored()
    {
        var (r, s, _, _, _) = Make();
        r.SetPhoneReady(W, H);
        r.OnRawMouseButton(HidMouseButtons.Right, true);
        Assert.Empty(s.Back);
    }

    [Fact]
    public void Returns_after_pushing_back_at_entry_edge()
    {
        var (r, s, c, g, k) = Make();
        r.SetPhoneReady(W, H);
        PushIn(r, k, y: 270); // 25% down
        r.OnRawMouseButton(HidMouseButtons.Left, true);
        r.OnRawKey(0x04, true);
        s.Mouse.Clear(); s.Keyboard.Clear();
        bool? mode = null; r.ModeChanged += m => mode = m;
        for (int t = 0; t <= 600; t += 50) { k.NowMs = 2000 + t; r.OnRawMouseMove(3, 0); }
        Assert.False(r.OnPhone);
        Assert.False(g.Swallow);
        Assert.Equal((1, 270), c.Pos);
        Assert.False(mode);
        Assert.Equal(0, s.Mouse[^1][0]);                        // buttons released
        Assert.Equal(KeyboardState.EmptyReport, s.Keyboard[^1]); // keys released
    }

    [Fact]
    public void ReturnToPc_is_idempotent_and_panic_safe()
    {
        var (r, s, _, g, k) = Make();
        r.ReturnToPc();
        Assert.False(g.Swallow);
        r.SetPhoneReady(W, H);
        PushIn(r, k);
        r.ReturnToPc();
        Assert.False(r.OnPhone);
        int n = s.Mouse.Count;
        r.ReturnToPc();
        Assert.Equal(n, s.Mouse.Count);
    }

    [Fact]
    public void Phone_unavailable_while_on_phone_returns_to_pc_without_sending()
    {
        var (r, s, _, g, k) = Make();
        r.SetPhoneReady(W, H);
        PushIn(r, k);
        int n = s.Mouse.Count;
        r.SetPhoneUnavailable();
        Assert.False(r.OnPhone);
        Assert.False(g.Swallow);
        Assert.False(r.PhoneReady);
        Assert.Equal(n, s.Mouse.Count);
    }

    [Fact]
    public void Keys_forwarded_only_on_phone_and_when_enabled()
    {
        var (r, s, _, _, k) = Make();
        r.SetPhoneReady(W, H);
        r.OnRawKey(0x04, true);
        Assert.Empty(s.Keyboard);
        PushIn(r, k);
        r.OnRawKey(0x04, true);
        Assert.Equal(0x04, s.Keyboard[^1][2]);
        r.OnRawKey(0xE0, true);
        Assert.Equal(1, s.Keyboard[^1][0]);
        r.OnRawKey(0x04, false);
        Assert.Equal(0, s.Keyboard[^1][2]);

        var (r2, s2, _, _, k2) = Make(AppConfig.Default with { ForwardKeyboard = false, EnterDwellMs = 1000 });
        r2.SetPhoneReady(W, H);
        PushIn(r2, k2);
        r2.OnRawKey(0x04, true);
        Assert.Empty(s2.Keyboard);
    }

    [Fact]
    public void UpdatePhoneBounds_reclamps_position()
    {
        var (r, s, _, _, k) = Make();
        r.SetPhoneReady(W, H);
        PushIn(r, k, y: 1079); // bottom → phone y = H-1
        r.UpdatePhoneBounds(H, W); // rotated
        s.Mouse.Clear();
        r.OnRawMouseMove(0, 1); // now at bottom (W-1) of the rotated display: nothing applied
        Assert.Empty(s.Mouse);
    }

    [Fact]
    public void UpdatePhoneBounds_while_on_phone_resyncs_real_cursor()
    {
        var (r, s, _, _, k) = Make();
        r.SetPhoneReady(W, H);
        PushIn(r, k); // default y = 540, Edge.Left → phone entry x = W - 1
        s.Mouse.Clear();
        r.UpdatePhoneBounds(H, W); // rotated: new width = H, new height = W
        int previousY = (int)Math.Round(540.0 / 1079 * (H - 1)); // same fraction math as EnterPhone
        int expectedX = Math.Min(W - 1, H - 1); // entry x (W - 1) re-clamped to new width (H); W < H so unchanged
        int expectedY = Math.Clamp(previousY, 0, W - 1); // previous y re-clamped to new height (W)
        int sx = s.Mouse.Sum(m => (int)(sbyte)m[1]);
        int sy = s.Mouse.Sum(m => (int)(sbyte)m[2]);
        Assert.Equal(-(H + 127) + expectedX, sx);
        Assert.Equal(-(W + 127) + expectedY, sy);
    }

    [Fact]
    public void ModeChanged_is_raised_outside_the_lock()
    {
        var (r, _, _, _, k) = Make();
        r.SetPhoneReady(W, H);
        bool otherThreadGotIn = false;
        r.ModeChanged += _ =>
        {
            var t = new Thread(() => { r.OnRawMouseButton(HidMouseButtons.Left, true); otherThreadGotIn = true; });
            t.Start();
            Assert.True(t.Join(TimeSpan.FromSeconds(2)), "a second thread must be able to enter the router while ModeChanged runs");
        };
        PushIn(r, k);
        Assert.True(r.OnPhone);
        Assert.True(otherThreadGotIn);
    }

    [Fact]
    public void Racing_return_during_enter_delivers_both_mode_changes_in_order()
    {
        var (r, _, _, _, k) = Make();
        r.SetPhoneReady(W, H);
        var seen = new List<bool>();
        var returned = new ManualResetEventSlim();
        r.ModeChanged += m =>
        {
            lock (seen) seen.Add(m);
            if (m && !returned.IsSet)
            {
                // Simulate the session thread forcing a return while the enter notification is still running.
                var t = new Thread(() => { r.ReturnToPc(); returned.Set(); });
                t.Start();
                Assert.True(t.Join(TimeSpan.FromSeconds(2)));
            }
        };
        PushIn(r, k);
        Assert.False(r.OnPhone);
        lock (seen) Assert.Equal(new[] { true, false }, seen);
    }

    [Fact]
    public void Concurrent_flushes_deliver_mode_changes_in_queue_order()
    {
        var (r, _, _, _, k) = Make();
        r.SetPhoneReady(W, H);
        var seen = new List<bool>();
        var firstHandlerEntered = new ManualResetEventSlim();
        var releaseFirstHandler = new ManualResetEventSlim();
        r.ModeChanged += m =>
        {
            lock (seen) seen.Add(m);
            if (m) { firstHandlerEntered.Set(); releaseFirstHandler.Wait(TimeSpan.FromSeconds(2)); }
        };
        var enter = new Thread(() => PushIn(r, k));
        enter.Start();
        Assert.True(firstHandlerEntered.Wait(TimeSpan.FromSeconds(2)));
        // While thread A is inside ModeChanged(true), thread B returns to PC and flushes; it must not deliver false ahead of A.
        var ret = new Thread(() => r.ReturnToPc());
        ret.Start();
        Assert.True(ret.Join(TimeSpan.FromSeconds(2)));
        lock (seen) Assert.Equal(new[] { true }, seen); // false is queued, not yet delivered
        releaseFirstHandler.Set();
        Assert.True(enter.Join(TimeSpan.FromSeconds(2)));
        lock (seen) Assert.Equal(new[] { true, false }, seen);
        Assert.False(r.OnPhone);
    }
}
