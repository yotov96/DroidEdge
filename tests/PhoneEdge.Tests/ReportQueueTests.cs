using System.Diagnostics;
using PhoneEdge.Core.Hid;
using PhoneEdge.Core.Input;
using PhoneEdge.Core.Protocol;

namespace PhoneEdge.Tests;

public class ReportQueueTests
{
    private static byte[] Move(int dx, int dy, HidMouseButtons b = HidMouseButtons.None) => MouseReport.Encode(b, (sbyte)dx, (sbyte)dy, 0, 0);

    [Fact]
    public void Take_returns_framed_uhid_input()
    {
        var q = new ReportQueue();
        q.EnqueueMouse(Move(1, 2));
        var framed = q.Take(CancellationToken.None);
        Assert.Equal(ControlMessages.UhidInput(HidDescriptors.MouseId, Move(1, 2)), framed);
    }

    [Fact]
    public void Consecutive_moves_with_same_buttons_coalesce_with_saturation()
    {
        var q = new ReportQueue();
        q.EnqueueMouse(Move(100, -5));
        q.EnqueueMouse(Move(50, -5));
        q.EnqueueMouse(Move(1, 0));
        Assert.Equal(2, q.Count); // 127 saturated → remainder starts a new item
        var first = q.Take(CancellationToken.None)!;
        Assert.Equal((byte)127, first[5 + 1]);
        Assert.Equal(unchecked((byte)-10), first[5 + 2]);
        var second = q.Take(CancellationToken.None)!;
        Assert.Equal((byte)24, second[5 + 1]);
    }

    [Fact]
    public void Button_change_and_wheel_do_not_coalesce()
    {
        var q = new ReportQueue();
        q.EnqueueMouse(Move(1, 0));
        q.EnqueueMouse(Move(1, 0, HidMouseButtons.Left));
        q.EnqueueMouse(MouseReport.Encode(HidMouseButtons.Left, 0, 0, 1, 0));
        q.EnqueueMouse(MouseReport.Encode(HidMouseButtons.Left, 0, 0, 1, 0));
        Assert.Equal(4, q.Count);
    }

    [Fact]
    public void Keyboard_reports_are_framed_with_keyboard_id_and_never_coalesce()
    {
        var q = new ReportQueue();
        var r = new byte[8]; r[2] = 0x04;
        q.EnqueueKeyboard(r);
        q.EnqueueKeyboard(r);
        Assert.Equal(2, q.Count);
        Assert.Equal(ControlMessages.UhidInput(HidDescriptors.KeyboardId, r), q.Take(CancellationToken.None));
    }

    [Fact]
    public void Full_queue_evicts_oldest_coalescable_before_dropping()
    {
        var q = new ReportQueue(capacity: 3);
        q.EnqueueMouse(Move(127, 0));                       // coalescable A
        q.EnqueueMouse(Move(127, 0));                       // saturates tail A, remainder starts coalescable B
        q.EnqueueKeyboard(new byte[8]);                     // non-coalescable
        Assert.True(q.EnqueueKeyboard(new byte[8]));        // evicts A
        Assert.Equal(3, q.Count);
        Assert.True(q.EnqueueKeyboard(new byte[8]));        // evicts B
        Assert.False(q.EnqueueKeyboard(new byte[8]));       // nothing left to evict → dropped
        Assert.Equal(3, q.Count);
    }

    [Fact]
    public void Button_transitions_are_never_evicted()
    {
        var q = new ReportQueue(capacity: 2);
        q.EnqueueMouse(Move(0, 0, HidMouseButtons.Left)); // press
        q.EnqueueMouse(Move(0, 0));                       // release
        Assert.False(q.EnqueueKeyboard(new byte[8]));     // both are button transitions; nothing to evict → dropped
        Assert.Equal(2, q.Count);
    }

    [Fact]
    public void Take_returns_null_when_cancelled()
    {
        var q = new ReportQueue();
        using var cts = new CancellationTokenSource(50);
        Assert.Null(q.Take(cts.Token));
    }

    [Fact]
    public void EnqueueRaw_is_passed_through_unframed()
    {
        var q = new ReportQueue();
        var raw = ControlMessages.UhidDestroy(1);
        Assert.True(q.EnqueueRaw(raw));
        Assert.Same(raw, q.Take(CancellationToken.None));
    }

    [Fact]
    public void EnqueueRaw_gives_up_after_timeout()
    {
        var q = new ReportQueue(capacity: 1);
        q.EnqueueKeyboard(new byte[8]); // fills the only slot with a non-coalescable item
        var sw = Stopwatch.StartNew();
        Assert.False(q.EnqueueRaw(ControlMessages.UhidDestroy(1), TimeSpan.FromMilliseconds(100)));
        sw.Stop();
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2));
    }
}
