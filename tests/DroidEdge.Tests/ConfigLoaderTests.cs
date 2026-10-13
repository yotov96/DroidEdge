using DroidEdge.Core;
using DroidEdge.Core.Config;

namespace DroidEdge.Tests;

public class ConfigLoaderTests
{
    [Fact]
    public void Empty_json_yields_defaults()
    {
        var cfg = ConfigLoader.Parse("{}", _ => { });
        Assert.Equal(AppConfig.Default, cfg);
        Assert.Equal(Edge.Left, cfg.Edge);
        Assert.Equal("primary", cfg.Monitor);
        Assert.Equal(1000, cfg.EnterDwellMs);
        Assert.Equal(1000, cfg.ExitDwellMs);
        Assert.Equal(1.0, cfg.Sensitivity);
        Assert.True(cfg.ForwardKeyboard);
        Assert.True(cfg.RightClickIsBack);
        Assert.True(cfg.MiddleClickIsDone);
        Assert.True(cfg.SyncClipboard);
        Assert.True(cfg.OpenDesktopOnConnect);
        Assert.Equal("1920x1080", cfg.DesktopDisplay);
        Assert.True(cfg.DesktopFlex);
        Assert.Equal(new[] { "LControl", "RControl" }, cfg.PanicHotkey);
    }

    [Fact]
    public void Desktop_options_are_parsed_and_validated()
    {
        var cfg = ConfigLoader.Parse("""{"syncClipboard":false,"openDesktopOnConnect":false,"desktopDisplay":"1440x1800/240","desktopFlex":false}""", _ => { });
        Assert.False(cfg.SyncClipboard);
        Assert.False(cfg.OpenDesktopOnConnect);
        Assert.Equal("1440x1800/240", cfg.DesktopDisplay);
        Assert.False(cfg.DesktopFlex);
        Assert.Equal("/200", ConfigLoader.Parse("""{"desktopDisplay":"/200"}""", _ => { }).DesktopDisplay);
        Assert.Equal("", ConfigLoader.Parse("""{"desktopDisplay":""}""", _ => { }).DesktopDisplay);

        var warnings = new List<string>();
        Assert.Equal("1920x1080", ConfigLoader.Parse("""{"desktopDisplay":"big"}""", warnings.Add).DesktopDisplay);
        Assert.Single(warnings);
    }

    [Fact]
    public void Desktop_window_defaults_capture_no_mouse_but_keep_the_other_customizations()
    {
        AppConfig cfg = ConfigLoader.Parse("{}", _ => { });
        Assert.Equal("uhid", cfg.DesktopKeyboard);
        Assert.Equal("sdk", cfg.DesktopMouse);
        Assert.Equal("rctrl", cfg.DesktopShortcutMod);
        Assert.True(cfg.DesktopTildeReleasesMouse);
        Assert.True(cfg.DesktopKeepAwake);
        Assert.True(cfg.DesktopKeepAppsOnClose);
        Assert.True(cfg.DesktopPatchedServer);
        Assert.True(cfg.DesktopPatchedClient);
    }

    [Fact]
    public void Desktop_window_customizations_can_be_turned_off()
    {
        List<string> warnings = [];
        AppConfig cfg = ConfigLoader.Parse("""
            {"desktopKeyboard":"SDK","desktopMouse":"sdk","desktopShortcutMod":" LAlt , lsuper ","desktopTildeReleasesMouse":false,
             "desktopKeepAwake":false,"desktopKeepAppsOnClose":false,"desktopPatchedServer":false,"desktopPatchedClient":false}
            """, warnings.Add);
        Assert.Empty(warnings);
        Assert.Equal("sdk", cfg.DesktopKeyboard);
        Assert.Equal("sdk", cfg.DesktopMouse);
        Assert.Equal("lalt,lsuper", cfg.DesktopShortcutMod);
        Assert.False(cfg.DesktopTildeReleasesMouse);
        Assert.False(cfg.DesktopKeepAwake);
        Assert.False(cfg.DesktopKeepAppsOnClose);
        Assert.False(cfg.DesktopPatchedServer);
        Assert.False(cfg.DesktopPatchedClient);
        Assert.NotEqual(AppConfig.Default, cfg);
    }

    [Fact]
    public void Invalid_desktop_input_settings_fall_back_and_warn()
    {
        List<string> warnings = [];
        AppConfig cfg = ConfigLoader.Parse("""{"desktopKeyboard":"aoa","desktopMouse":"","desktopShortcutMod":"lctrl,tilde"}""", warnings.Add);
        Assert.Equal("uhid", cfg.DesktopKeyboard);
        Assert.Equal("sdk", cfg.DesktopMouse);
        Assert.Equal("rctrl", cfg.DesktopShortcutMod);
        Assert.Equal(3, warnings.Count);
        Assert.Equal("rctrl", ConfigLoader.Parse("""{"desktopShortcutMod":" , "}""", _ => { }).DesktopShortcutMod);
    }

    [Fact]
    public void Partial_json_overrides_only_given_fields()
    {
        var cfg = ConfigLoader.Parse("""{"edge":"right","enterDwellMs":250,"sensitivity":1.5,"rightClickIsBack":false,"middleClickIsDone":false}""", _ => { });
        Assert.Equal(Edge.Right, cfg.Edge);
        Assert.Equal(250, cfg.EnterDwellMs);
        Assert.Equal(1.5, cfg.Sensitivity);
        Assert.Equal(1000, cfg.ExitDwellMs);
        Assert.False(cfg.RightClickIsBack);
        Assert.False(cfg.MiddleClickIsDone);
    }

    [Fact]
    public void Invalid_values_fall_back_and_warn()
    {
        var warnings = new List<string>();
        var cfg = ConfigLoader.Parse("""{"edge":"diagonal","enterDwellMs":-5,"sensitivity":0}""", warnings.Add);
        Assert.Equal(Edge.Left, cfg.Edge);
        Assert.Equal(1000, cfg.EnterDwellMs);
        Assert.Equal(1.0, cfg.Sensitivity);
        Assert.Equal(3, warnings.Count);
    }

    [Fact]
    public void Malformed_json_yields_defaults_and_warns()
    {
        var warnings = new List<string>();
        var cfg = ConfigLoader.Parse("{ not json", warnings.Add);
        Assert.Equal(AppConfig.Default, cfg);
        Assert.Single(warnings);
    }

    [Fact]
    public void Missing_file_yields_defaults_without_warning()
    {
        var warnings = new List<string>();
        var cfg = ConfigLoader.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"), warnings.Add);
        Assert.Equal(AppConfig.Default, cfg);
        Assert.Empty(warnings);
    }
}
