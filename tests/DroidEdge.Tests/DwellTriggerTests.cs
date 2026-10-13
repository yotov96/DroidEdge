using DroidEdge.Core.Input;

namespace DroidEdge.Tests;

public class DwellTriggerTests
{
    [Fact]
    public void Fires_once_after_continuous_pushing()
    {
        var d = new DwellTrigger(1000);
        for (int t = 0; t < 1000; t += 100) Assert.False(d.Sample(true, t)); // 0..900
        Assert.True(d.Sample(true, 1000));
        Assert.False(d.Sample(true, 1100)); // reset after firing; new dwell starts
    }

    [Fact]
    public void Non_pushing_sample_resets()
    {
        var d = new DwellTrigger(1000);
        d.Sample(true, 0);
        d.Sample(false, 600); // reset
        for (int t = 1100; t < 2100; t += 100) Assert.False(d.Sample(true, t));
        Assert.True(d.Sample(true, 2100));
    }

    [Fact]
    public void Gap_between_pushing_samples_restarts_timer()
    {
        var d = new DwellTrigger(1000, maxGapMs: 150);
        d.Sample(true, 0);
        d.Sample(true, 100);
        Assert.False(d.Sample(true, 900)); // gap of 800 → restart at 900
        for (int t = 1000; t < 1900; t += 100) Assert.False(d.Sample(true, t));
        Assert.True(d.Sample(true, 1900));
    }

    [Fact]
    public void Zero_dwell_fires_on_first_push()
    {
        Assert.True(new DwellTrigger(0).Sample(true, 42));
    }

    [Fact]
    public void Gap_after_dwell_elapsed_restarts_instead_of_firing()
    {
        var d = new DwellTrigger(200, maxGapMs: 150);
        Assert.False(d.Sample(true, 0));
        Assert.False(d.Sample(true, 1000)); // 1000 ms gap → restart, must not fire
        Assert.False(d.Sample(true, 1100));
        Assert.True(d.Sample(true, 1200));
    }
}
