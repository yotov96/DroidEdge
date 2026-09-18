using PhoneEdge.Phone;

namespace PhoneEdge.Tests;

public class ScrcpyInstallTests
{
    private static string Dir()
    {
        var d = Path.Combine(Path.GetTempPath(), "phoneedge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact]
    public void Missing_files_are_reported()
    {
        var d = Dir();
        var err = ScrcpyInstall.Validate(d, _ => "scrcpy 4.1 <https://github.com/Genymobile/scrcpy>");
        Assert.NotNull(err);
        Assert.Contains("adb.exe", err);
    }

    [Fact]
    public void Matching_version_passes()
    {
        var d = Dir();
        File.WriteAllText(Path.Combine(d, "adb.exe"), "");
        File.WriteAllText(Path.Combine(d, "scrcpy-server"), "");
        File.WriteAllText(Path.Combine(d, "scrcpy.exe"), "");
        Assert.Null(ScrcpyInstall.Validate(d, _ => "scrcpy 4.1 <https://github.com/Genymobile/scrcpy>"));
    }

    [Fact]
    public void Different_version_is_rejected()
    {
        var d = Dir();
        File.WriteAllText(Path.Combine(d, "adb.exe"), "");
        File.WriteAllText(Path.Combine(d, "scrcpy-server"), "");
        File.WriteAllText(Path.Combine(d, "scrcpy.exe"), "");
        var err = ScrcpyInstall.Validate(d, _ => "scrcpy 4.2 <https://github.com/Genymobile/scrcpy>");
        Assert.NotNull(err);
        Assert.Contains("4.1", err);
        Assert.Contains("4.2", err);
    }

    [Fact]
    public void Unreadable_version_is_rejected()
    {
        var d = Dir();
        File.WriteAllText(Path.Combine(d, "adb.exe"), "");
        File.WriteAllText(Path.Combine(d, "scrcpy-server"), "");
        File.WriteAllText(Path.Combine(d, "scrcpy.exe"), "");
        Assert.NotNull(ScrcpyInstall.Validate(d, _ => null));
    }
}
