using DroidEdge.Core.Input;

namespace DroidEdge.Tests;

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
        t.SetBounds(2000, 1000, 1.0);
        Assert.Equal((900, 999), (t.X, t.Y));
    }

    [Fact]
    public void PixelsPerCountFor_scales_by_density_from_Android_17()
    {
        Assert.Equal(1.125, PointerTracker.PixelsPerCountFor(37, 360));
        Assert.Equal(0.5, PointerTracker.PixelsPerCountFor(38, 160));
        Assert.Equal(1.0, PointerTracker.PixelsPerCountFor(36, 360));
        Assert.Equal(1.0, PointerTracker.PixelsPerCountFor(37, 0)); // density unknown
    }

    [Fact]
    public void Scaled_counts_keep_the_real_cursor_at_or_inside_the_tracked_position()
    {
        var t = new PointerTracker(1008, 2244, 1.0, 1.125);
        t.SetPosition(0, 0);
        int sumX = 0, sumY = 0;
        for (int i = 0; i < 100; i++)
        {
            var (cx, cy) = t.Move(37, 53);
            sumX += cx;
            sumY += cy;
            Assert.InRange(sumX * 1.125, t.X - 1.125, t.X);
            Assert.InRange(sumY * 1.125, t.Y - 1.125, t.Y);
        }
        Assert.True(t.AtRight && t.AtBottom);
        Assert.Equal((0, 0), t.Move(50, 50)); // pinned at the edge: the real cursor gets no further
    }

    [Fact]
    public void Resync_overshoot_reaches_the_corner_at_any_scale()
    {
        var t = new PointerTracker(1000, 2000, 1.0, 0.5); // each count moves the cursor half a pixel
        t.SetPosition(10, 20);
        var moves = t.Resync().ToArray();
        var push = moves.TakeWhile(m => m.dx < 0 || m.dy < 0).ToArray();
        Assert.True(push.Sum(m => m.dx) * 0.5 <= -1000);
        Assert.True(push.Sum(m => m.dy) * 0.5 <= -2000);
        Assert.Equal(-(2000 + 127) + 20, moves.Sum(m => m.dx));
        Assert.Equal(-(4000 + 127) + 40, moves.Sum(m => m.dy));
    }

    [Fact]
    public void Resync_at_scale_1_matches_the_unscaled_moves()
    {
        var t = new PointerTracker(1344, 2992, 1.0);
        t.SetPosition(1343, 900);
        Assert.Equal(MoveChunker.Resync(1344, 2992, 1343, 900), t.Resync());
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
