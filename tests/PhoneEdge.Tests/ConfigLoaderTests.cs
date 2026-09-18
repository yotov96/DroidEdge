using PhoneEdge.Core;
using PhoneEdge.Core.Config;

namespace PhoneEdge.Tests;

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
        Assert.Equal(new[] { "LControl", "RControl" }, cfg.PanicHotkey);
    }

    [Fact]
    public void Partial_json_overrides_only_given_fields()
    {
        var cfg = ConfigLoader.Parse("""{"edge":"right","enterDwellMs":250,"sensitivity":1.5,"rightClickIsBack":false}""", _ => { });
        Assert.Equal(Edge.Right, cfg.Edge);
        Assert.Equal(250, cfg.EnterDwellMs);
        Assert.Equal(1.5, cfg.Sensitivity);
        Assert.Equal(1000, cfg.ExitDwellMs);
        Assert.False(cfg.RightClickIsBack);
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
