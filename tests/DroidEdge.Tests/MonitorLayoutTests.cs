using DroidEdge.Core;
using DroidEdge.Win;

namespace DroidEdge.Tests;

public class MonitorLayoutTests
{
    private static readonly IntRect Primary = new(0, 0, 1920, 1080);

    [Fact]
    public void Left_edge_with_nothing_adjacent_is_full_height_one_pixel_wide()
    {
        var seg = MonitorLayout.EdgeSegment(Edge.Left, Primary, []);
        Assert.Equal(new IntRect(0, 0, 1, 1080), seg);
    }

    [Fact]
    public void Adjacent_monitor_blocks_the_overlapping_span()
    {
        var leftMon = new IntRect(-1920, 200, 0, 1280); // overlaps y 200..1080
        var seg = MonitorLayout.EdgeSegment(Edge.Left, Primary, [leftMon]);
        Assert.Equal(new IntRect(0, 0, 1, 200), seg);
        var fullyBlocked = MonitorLayout.EdgeSegment(Edge.Left, Primary, [new IntRect(-1920, 0, 0, 1080)]);
        Assert.Null(fullyBlocked);
    }

    [Fact]
    public void Right_top_bottom_segments()
    {
        Assert.Equal(new IntRect(1919, 0, 1920, 1080), MonitorLayout.EdgeSegment(Edge.Right, Primary, []));
        Assert.Equal(new IntRect(0, 0, 1920, 1), MonitorLayout.EdgeSegment(Edge.Top, Primary, []));
        Assert.Equal(new IntRect(0, 1079, 1920, 1080), MonitorLayout.EdgeSegment(Edge.Bottom, Primary, []));
        Assert.Equal(new IntRect(1000, 1079, 1920, 1080), MonitorLayout.EdgeSegment(Edge.Bottom, Primary, [new IntRect(0, 1080, 1000, 2000)]));
    }

    [Fact]
    public void Select_by_primary_or_index()
    {
        var mons = new List<IntRect> { Primary, new(1920, 0, 3840, 1080) };
        Assert.Equal(Primary, MonitorLayout.Select(mons, "primary"));
        Assert.Equal(mons[1], MonitorLayout.Select(mons, "1"));
        Assert.Equal(Primary, MonitorLayout.Select(mons, "7")); // out of range → primary
    }
}
