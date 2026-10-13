using System.Text.Json;
using DroidEdge.Core.Config;

namespace DroidEdge.Tests;

public sealed class ConfigSetValueTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"droidedge-test-{Guid.NewGuid()}.json");

    public void Dispose() => File.Delete(_path);

    [Fact]
    public void Creates_the_file_when_missing()
    {
        ConfigLoader.SetValue(_path, "desktopMouse", "uhid");
        Assert.Equal("uhid", ConfigLoader.Load(_path, _ => { }).DesktopMouse);
    }

    [Fact]
    public void Keeps_other_settings_and_replaces_any_spelling_of_the_name()
    {
        File.WriteAllText(_path, """
            {
              // comments and trailing commas are accepted when reading
              "edge": "right",
              "DesktopMouse": "uhid",
              "openDesktopOnConnect": false,
            }
            """);
        ConfigLoader.SetValue(_path, "desktopMouse", "sdk");

        AppConfig cfg = ConfigLoader.Load(_path, _ => { });
        Assert.Equal("sdk", cfg.DesktopMouse);
        Assert.Equal(DroidEdge.Core.Edge.Right, cfg.Edge);
        Assert.False(cfg.OpenDesktopOnConnect);
        Assert.DoesNotContain("DesktopMouse", File.ReadAllText(_path));
    }

    [Fact]
    public void Leaves_a_broken_file_unchanged()
    {
        File.WriteAllText(_path, "{ not json");
        Assert.ThrowsAny<JsonException>(() => ConfigLoader.SetValue(_path, "desktopMouse", "uhid"));
        Assert.Equal("{ not json", File.ReadAllText(_path));
    }
}
