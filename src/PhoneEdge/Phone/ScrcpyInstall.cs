using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PhoneEdge.Phone;

public static partial class ScrcpyInstall
{
    public const string ExpectedVersion = "4.1";

    public static string? Validate(string scrcpyDir, Func<string, string?> runVersion)
    {
        var missing = new[] { "adb.exe", "scrcpy-server", "scrcpy.exe" }.Where(f => !File.Exists(Path.Combine(scrcpyDir, f))).ToList();
        if (missing.Count > 0) return $"scrcpy folder '{scrcpyDir}' is missing: {string.Join(", ", missing)}";
        var line = runVersion(Path.Combine(scrcpyDir, "scrcpy.exe"));
        if (line is null) return $"Could not run scrcpy.exe --version in '{scrcpyDir}'";
        var m = VersionRegex().Match(line);
        if (!m.Success) return $"Could not parse scrcpy version from '{line}'";
        if (m.Groups[1].Value != ExpectedVersion) return $"PhoneEdge requires scrcpy {ExpectedVersion} but found {m.Groups[1].Value} in '{scrcpyDir}'";
        return null;
    }

    public static string? RunVersion(string exePath)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(exePath, "--version") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
            if (p is null) return null;
            var first = p.StandardOutput.ReadLine();
            p.WaitForExit(5000);
            return first;
        }
        catch { return null; }
    }

    [GeneratedRegex(@"scrcpy\s+(\d+\.\d+)")]
    private static partial Regex VersionRegex();
}
