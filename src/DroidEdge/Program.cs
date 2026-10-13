using DroidEdge.App;
using DroidEdge.Core.Config;
using DroidEdge.Logging;
using Serilog;

namespace DroidEdge;

internal static class Program
{
    public static readonly string BaseDir = AppContext.BaseDirectory;
    public static readonly string ConfigPath = Path.Combine(BaseDir, "droidedge.json");

    [STAThread]
    private static int Main(string[] args)
    {
        var warnings = new List<string>();
        var cfg = ConfigLoader.Load(ConfigPath, warnings.Add);
        var log = LogSetup.Create(BaseDir, cfg.LogLevel);
        foreach (var w in warnings) log.Warning("{Warning}", w);
        log.Information("DroidEdge starting (args: {Args})", string.Join(' ', args));

        ApplicationConfiguration.Initialize();

        if (args.Contains("--diag", StringComparer.OrdinalIgnoreCase))
        {
            var report = Diagnostics.RunAsync(cfg, log, CancellationToken.None).GetAwaiter().GetResult();
            File.WriteAllText(Path.Combine(BaseDir, "droidedge-diag.txt"), report);
            MessageBox.Show(report, "DroidEdge diagnostics", MessageBoxButtons.OK, report.StartsWith("PASS") ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            return report.StartsWith("PASS") ? 0 : 1;
        }

        using var single = new Mutex(initiallyOwned: true, @"Local\DroidEdge-single-instance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("DroidEdge is already running (see the tray).", "DroidEdge", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 2;
        }

        TrayApp? app = null;
        Application.ThreadException += (_, e) => { log.Error(e.Exception, "Unhandled UI exception"); app?.EmergencyRelease(); };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => { log.Fatal(e.ExceptionObject as Exception, "Unhandled exception"); app?.EmergencyRelease(); };
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        try
        {
            app = new TrayApp(cfg, log, warnings);
            Application.Run(app);
        }
        catch (Exception ex)
        {
            log.Fatal(ex, "Startup failed");
            MessageBox.Show(ex.Message, "DroidEdge failed to start", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
            (log as IDisposable)?.Dispose();
        }
        return 0;
    }
}
