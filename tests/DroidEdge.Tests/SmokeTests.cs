using DroidEdge.Core;

namespace DroidEdge.Tests;

public class SmokeTests
{
    [Fact]
    public void IntRect_computes_width_and_height()
    {
        var r = new IntRect(10, 20, 110, 70);
        Assert.Equal(100, r.Width);
        Assert.Equal(50, r.Height);
        Assert.True(r.Contains(10, 20));
        Assert.False(r.Contains(110, 20));
    }
}
