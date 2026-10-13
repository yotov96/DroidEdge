using System.Text.RegularExpressions;

namespace DroidEdge.Phone;

public sealed record AdbDevice(string Serial, string State);

/// <summary>Current oriented logical display size, rotation (0..3) and density (0 when unknown).</summary>
public sealed record DisplayInfo(int Width, int Height, int Rotation, int DensityDpi);

public static partial class AdbParsers
{
    public static List<AdbDevice> ParseDevices(string output)
    {
        var list = new List<AdbDevice>();
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("List of devices", StringComparison.Ordinal) || line.StartsWith('*')) continue;
            var parts = line.Split((char[])[' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) list.Add(new AdbDevice(parts[0], parts[1]));
        }
        return list;
    }

    public static (int w, int h)? ParseWmSize(string output)
    {
        var over = WmSizeRegex().Matches(output).FirstOrDefault(m => m.Groups[1].Value == "Override");
        var phys = WmSizeRegex().Matches(output).FirstOrDefault(m => m.Groups[1].Value == "Physical");
        var m = over ?? phys;
        if (m is null) return null;
        return (int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
    }

    public static DisplayInfo? ParseViewport(string dumpsysInput)
    {
        foreach (var raw in dumpsysInput.Split('\n'))
        {
            if (!raw.Contains("Viewport", StringComparison.Ordinal) || !raw.Contains("displayId=0,", StringComparison.Ordinal)) continue;
            var o = OrientationRegex().Match(raw);
            var f = LogicalFrameRegex().Match(raw);
            if (!o.Success || !f.Success) continue;
            int l = int.Parse(f.Groups[1].Value), t = int.Parse(f.Groups[2].Value), r = int.Parse(f.Groups[3].Value), b = int.Parse(f.Groups[4].Value);
            if (r - l <= 0 || b - t <= 0) continue;
            var d = DensityRegex().Match(raw);
            return new DisplayInfo(r - l, b - t, int.Parse(o.Groups[1].Value), d.Success ? int.Parse(d.Groups[1].Value) : 0);
        }
        return null;
    }

    public static string? ParseSettingValue(string output)
    {
        var v = output.Trim();
        return v.Length == 0 || v == "null" ? null : v;
    }

    [GeneratedRegex(@"(Physical|Override) size:\s*(\d+)x(\d+)")]
    private static partial Regex WmSizeRegex();

    [GeneratedRegex(@"orientation=(\d)")]
    private static partial Regex OrientationRegex();

    [GeneratedRegex(@"logicalFrame=\[(\d+),\s*(\d+),\s*(\d+),\s*(\d+)\]")]
    private static partial Regex LogicalFrameRegex();

    [GeneratedRegex(@"densityDpi=(\d+)")]
    private static partial Regex DensityRegex();
}
