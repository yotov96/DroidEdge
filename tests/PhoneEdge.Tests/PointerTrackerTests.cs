using PhoneEdge.Core.Input;

namespace PhoneEdge.Tests;

public class PointerTrackerTests
{
    [Fact]
    public void Move_accumulates_and_clamps()
    {
        var t = new PointerTracker(100, 200, 1.0);
        t.SetPosition(50, 50);
        Assert.Equal((10, -20), t.Move(10, -20));
        Assert.Equal((60, 30), (t.X, t.Y));
        Assert.Equal((39, -30), t.Move(500, -500)); // clamps to 99, 0
        Assert.Equal((99, 0), (t.X, t.Y));
        Assert.True(t.AtRight);
        Assert.True(t.AtTop);
        Assert.Equal((0, 0), t.Move(5, -5)); // already clamped: nothing applied
    }

    [Fact]
    public void Sensitivity_scales_and_keeps_fraction()
    {
        var t = new PointerTracker(1000, 1000, 0.5);
        t.SetPosition(0, 0);
        Assert.Equal((0, 0), t.Move(1, 1));  // 0.5 accumulated
        Assert.Equal((1, 1), t.Move(1, 1));  // 1.0 → applied
        Assert.Equal((1, 1), (t.X, t.Y));
        var t2 = new PointerTracker(1000, 1000, 2.0);
        t2.SetPosition(0, 0);
        Assert.Equal((6, 0), t2.Move(3, 0));
    }

    [Fact]
    public void SetBounds_reclamps_position()
    {
        var t = new PointerTracker(1000, 2000, 1.0);
        t.SetPosition(900, 1900);
        t.SetBounds(2000, 1000);
        Assert.Equal((900, 999), (t.X, t.Y));
    }

    [Fact]
    public void SetPosition_clears_fraction()
    {
        var t = new PointerTracker(100, 100, 0.5);
        t.Move(1, 1);
        t.SetPosition(10, 10);
        Assert.Equal((0, 0), t.Move(1, 1));
    }
}
