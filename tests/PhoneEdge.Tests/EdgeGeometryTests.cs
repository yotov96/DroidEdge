using PhoneEdge.Core;
using PhoneEdge.Core.Input;

namespace PhoneEdge.Tests;

public class EdgeGeometryTests
{
    private static readonly IntRect Mon = new(0, 0, 1920, 1080);

    [Fact]
    public void Left_edge_pushing_requires_x_zero_inside_segment_and_negative_dx()
    {
        var seg = new IntRect(0, 0, 1, 1080);
        Assert.True(EdgeGeometry.IsPushingOut(Edge.Left, seg, 0, 500, -3, 0));
        Assert.False(EdgeGeometry.IsPushingOut(Edge.Left, seg, 0, 500, 0, 5));
        Assert.False(EdgeGeometry.IsPushingOut(Edge.Left, seg, 1, 500, -3, 0));
        Assert.False(EdgeGeometry.IsPushingOut(Edge.Left, new IntRect(0, 0, 1, 500), 0, 700, -3, 0));
    }

    [Fact]
    public void Cursor_reported_beyond_the_edge_still_counts_as_pushing()
    {
        // WH_MOUSE_LL reports the unclipped position: pushing past the left edge yields negative x.
        var seg = new IntRect(0, 0, 1, 1080);
        Assert.True(EdgeGeometry.IsPushingOut(Edge.Left, seg, -3, 500, -10, 0));
        Assert.False(EdgeGeometry.IsPushingOut(Edge.Left, seg, -3, 1200, -10, 0)); // outside the segment span
        Assert.True(EdgeGeometry.IsPushingOut(Edge.Right, new IntRect(1919, 0, 1920, 1080), 1925, 10, 4, 0));
        Assert.True(EdgeGeometry.IsPushingOut(Edge.Top, new IntRect(0, 0, 1920, 1), 10, -7, 0, -2));
        Assert.True(EdgeGeometry.IsPushingOut(Edge.Bottom, new IntRect(0, 1079, 1920, 1080), 10, 1085, 0, 3));
    }

    [Fact]
    public void DropZoneBounds_sit_flush_on_the_edge_centred_on_the_cursor_and_inside_the_monitor()
    {
        Assert.Equal(new IntRect(0, 470, 200, 610), EdgeGeometry.DropZoneBounds(Edge.Left, Mon, -4, 540, 200, 140));
        Assert.Equal(new IntRect(0, 0, 200, 140), EdgeGeometry.DropZoneBounds(Edge.Left, Mon, 0, 10, 200, 140));      // clamped at the top
        Assert.Equal(new IntRect(0, 940, 200, 1080), EdgeGeometry.DropZoneBounds(Edge.Left, Mon, 0, 1075, 200, 140)); // clamped at the bottom
        Assert.Equal(new IntRect(1720, 470, 1920, 610), EdgeGeometry.DropZoneBounds(Edge.Right, Mon, 1925, 540, 200, 140));
        Assert.Equal(new IntRect(860, 0, 1060, 140), EdgeGeometry.DropZoneBounds(Edge.Top, Mon, 960, -3, 200, 140));
        Assert.Equal(new IntRect(860, 940, 1060, 1080), EdgeGeometry.DropZoneBounds(Edge.Bottom, Mon, 960, 1083, 200, 140));
    }

    [Fact]
    public void Right_top_bottom_edges()
    {
        Assert.True(EdgeGeometry.IsPushingOut(Edge.Right, new IntRect(1919, 0, 1920, 1080), 1919, 10, 2, 0));
        Assert.True(EdgeGeometry.IsPushingOut(Edge.Top, new IntRect(0, 0, 1920, 1), 10, 0, 0, -1));
        Assert.True(EdgeGeometry.IsPushingOut(Edge.Bottom, new IntRect(0, 1079, 1920, 1080), 10, 1079, 0, 1));
        Assert.False(EdgeGeometry.IsPushingOut(Edge.Bottom, new IntRect(0, 1079, 1920, 1080), 10, 1079, 0, -1));
    }

    [Fact]
    public void Fraction_and_PointOnEdge_round_trip()
    {
        Assert.Equal(0.5, EdgeGeometry.Fraction(Edge.Left, Mon, 0, 540), 3);
        Assert.Equal(0.25, EdgeGeometry.Fraction(Edge.Top, Mon, 480, 0), 3);
        Assert.Equal((1, 540), EdgeGeometry.PointOnEdge(Edge.Left, Mon, 0.5, 1));
        Assert.Equal((1918, 540), EdgeGeometry.PointOnEdge(Edge.Right, Mon, 0.5, 1));
        Assert.Equal((480, 1), EdgeGeometry.PointOnEdge(Edge.Top, Mon, 0.25, 1));
        Assert.Equal((480, 1078), EdgeGeometry.PointOnEdge(Edge.Bottom, Mon, 0.25, 1));
        Assert.Equal((0, 1079), EdgeGeometry.PointOnEdge(Edge.Left, Mon, 1.0, 0));
    }

    [Fact]
    public void PhoneEntry_lands_on_opposite_phone_edge()
    {
        Assert.Equal((1343, 1496), EdgeGeometry.PhoneEntry(Edge.Left, 0.5, 1344, 2992));
        Assert.Equal((0, 1496), EdgeGeometry.PhoneEntry(Edge.Right, 0.5, 1344, 2992));
        Assert.Equal((672, 2991), EdgeGeometry.PhoneEntry(Edge.Top, 0.5, 1344, 2992));
        Assert.Equal((672, 0), EdgeGeometry.PhoneEntry(Edge.Bottom, 0.5, 1344, 2992));
    }

    [Fact]
    public void IsPushingBack_and_PhoneFraction()
    {
        var p = new PointerTracker(1344, 2992, 1.0);
        p.SetPosition(1343, 748);
        Assert.True(EdgeGeometry.IsPushingBack(Edge.Left, p, 4, 0));
        Assert.False(EdgeGeometry.IsPushingBack(Edge.Left, p, -4, 0));
        Assert.Equal(0.25, EdgeGeometry.PhoneFraction(Edge.Left, p), 2);
        p.SetPosition(100, 748);
        Assert.False(EdgeGeometry.IsPushingBack(Edge.Left, p, 4, 0));
        p.SetPosition(0, 10);
        Assert.True(EdgeGeometry.IsPushingBack(Edge.Right, p, -1, 0));
        p.SetPosition(336, 2991);
        Assert.True(EdgeGeometry.IsPushingBack(Edge.Top, p, 0, 1));
        Assert.Equal(0.25, EdgeGeometry.PhoneFraction(Edge.Top, p), 2);
    }
}
