using PhoneEdge.App;
using PhoneEdge.Core.Config;

namespace PhoneEdge.Tests;

public class PhoneDesktopTests
{
    [Fact]
    public void Arguments_create_a_flexible_virtual_display_that_keeps_the_phone_awake()
    {
        var args = PhoneDesktop.BuildArguments("SER1", AppConfig.Default);
        Assert.Equal(new[]
        {
            "-s", "SER1", "--new-display=1920x1080", "--flex-display", "--stay-awake", "--keyboard=uhid", "--mouse=uhid", "--shortcut-mod=rctrl", "--no-audio",
            "--no-clipboard-autosync", "--no-vd-destroy-content", "--window-title=Phone desktop",
        }, args);
    }

    [Fact]
    public void Arguments_follow_the_configured_display_and_flex_setting()
    {
        var args = PhoneDesktop.BuildArguments("SER1", AppConfig.Default with { DesktopDisplay = "1440x1800/240", DesktopFlex = false });
        Assert.Contains("--new-display=1440x1800/240", args);
        Assert.DoesNotContain("--flex-display", args);
        Assert.Contains("--new-display", PhoneDesktop.BuildArguments("SER1", AppConfig.Default with { DesktopDisplay = "" }));
    }
}
