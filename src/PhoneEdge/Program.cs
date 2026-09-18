using PhoneEdge.App;
using PhoneEdge.Core.Config;
using PhoneEdge.Logging;

namespace PhoneEdge;

internal static class Program
{
    public static readonly string BaseDir = AppContext.BaseDirectory;
    public static readonly string ConfigPath = Path.Combine(BaseDir, "phoneedge.json");

    [STAThread]
    private static int Main(string[] args)
    {
        var warnings = new List<string>();
        var cfg = ConfigLoader.Load(ConfigPath, warnings.Add);
        var log = LogSetup.Create(BaseDir, cfg.LogLevel);
        foreach (var w in warnings) log.Warning("{Warning}", w);

        if (args.Contains("--diag", StringComparer.OrdinalIgnoreCase))
        {
            var report = Diagnostics.RunAsync(cfg, log, CancellationToken.None).GetAwaiter().GetResult();
            var path = Path.Combine(BaseDir, "phoneedge-diag.txt");
            File.WriteAllText(path, report);
            MessageBox.Show(report, "PhoneEdge diagnostics", MessageBoxButtons.OK, report.StartsWith("PASS") ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            return report.StartsWith("PASS") ? 0 : 1;
        }

        return 0; // tray app is wired in Task 14
    }
}
