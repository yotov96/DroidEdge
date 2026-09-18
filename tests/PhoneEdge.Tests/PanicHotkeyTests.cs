using PhoneEdge.Win;

namespace PhoneEdge.Tests;

public class PanicHotkeyTests
{
    [Fact]
    public void Fires_when_all_keys_down_and_not_before()
    {
        var p = PanicHotkey.Parse(["LControl", "RControl"], _ => { });
        Assert.False(p.OnKey(0xA2, true));
        Assert.True(p.OnKey(0xA3, true));
        Assert.False(p.OnKey(0xA3, false));
        Assert.False(p.OnKey(0xA2, false));
    }

    [Fact]
    public void Unknown_names_warn_and_fall_back_to_default()
    {
        var warnings = new List<string>();
        var p = PanicHotkey.Parse(["Bogus"], warnings.Add);
        Assert.Single(warnings);
        Assert.Equal(new[] { 0xA2, 0xA3 }, p.VirtualKeys);
    }
}
