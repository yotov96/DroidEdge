using PhoneEdge.Core.Input;

namespace PhoneEdge.Tests;

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
        d.Sample(false, 600);
        Assert.False(d.Sample(true, 1100));
        Assert.True(d.Sample(true, 2100));
    }

    [Fact]
    public void Gap_between_pushing_samples_restarts_timer()
    {
        var d = new DwellTrigger(1000, maxGapMs: 150);
        d.Sample(true, 0);
        d.Sample(true, 100);
        Assert.False(d.Sample(true, 900)); // gap of 800 → restart at 900
        Assert.False(d.Sample(true, 1000));
        Assert.True(d.Sample(true, 1900));
    }

    [Fact]
    public void Zero_dwell_fires_on_first_push()
    {
        Assert.True(new DwellTrigger(0).Sample(true, 42));
    }
}
