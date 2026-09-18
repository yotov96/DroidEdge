using System.Diagnostics;
using PhoneEdge.Core;
using PhoneEdge.Core.Config;
using PhoneEdge.Core.Input;
using PhoneEdge.Phone;
using PhoneEdge.Win;
using Serilog;

namespace PhoneEdge.App;

public sealed class TrayApp : ApplicationContext
{
    private readonly AppConfig _cfg;
    private readonly ILogger _log;
    private readonly TrayIcon _tray;
    private LowLevelHooks _hooks = null!;
    private RawInputWindow _raw = null!;
    private readonly SwitchableSink _sink = new();
    private InputRouter _router = null!;
    private PhoneSession? _session;
    private SessionState _lastState = SessionState.Stopped;
    private bool _wasReady;
    private bool _exiting;
    private bool _diagnosticsRunning;

    public TrayApp(AppConfig cfg, ILogger log, IReadOnlyList<string> configWarnings)
    {
        _cfg = cfg;
        _log = log;

        _tray = new TrayIcon(
            onReturnMouse: () => _router!.ReturnToPc(),
            onDiagnostics: () => _ = RunDiagnosticsAsync(),
            onOpenLog: () => OpenPath(Path.Combine(Program.BaseDir, "phoneedge.log")),
            onOpenConfig: OpenConfig,
            onStartupToggle: on => { try { StartupRegistration.SetEnabled(on); } catch (Exception ex) { _log.Error(ex, "Startup toggle failed"); } },
            onExit: Exit);
        _tray.SetStartupChecked(StartupRegistration.IsEnabled());

        try
        {
            var monitors = MonitorLayout.GetMonitors();
            var monitor = MonitorLayout.Select(monitors, cfg.Monitor);
            var others = monitors.Where(m => m != monitor);
            var segment = MonitorLayout.EdgeSegment(cfg.Edge, monitor, others);
            if (segment is null)
            {
                _log.Warning("The {Edge} edge of the selected monitor is fully adjacent to another monitor; using the whole edge anyway", cfg.Edge);
                segment = MonitorLayout.EdgeSegment(cfg.Edge, monitor, []);
            }
            _log.Information("Monitor {Mon}, edge {Edge}, segment {Seg}", monitor, cfg.Edge, segment);

            var panic = PanicHotkey.Parse(cfg.PanicHotkey, w => _log.Warning("{W}", w));
            _hooks = new LowLevelHooks(panic, log);
            _router = new InputRouter(cfg, monitor, segment!.Value, new Win32Cursor(), _hooks, _sink, new SystemClock(), s => _log.Information("{Router}", s));
            _router.ModeChanged += onPhone => Ui(() =>
            {
                if (onPhone) _tray.SetStatus("On phone", TrayColor.Blue);
                else _tray.SetStatus(_lastStatus, _lastState == SessionState.Ready ? TrayColor.Green : TrayColor.Grey);
            });

            _raw = new RawInputWindow(log);
            _raw.MouseMoved += (dx, dy) => _router.OnRawMouseMove(dx, dy);
            _raw.MouseButton += (b, down) => _router.OnRawMouseButton(b, down);
            _raw.Wheel += (delta, horizontal) => _router.OnRawWheel(delta, horizontal);
            _hooks.Key += (usage, down) => _router.OnRawKey(usage, down);
            _hooks.CursorMoved += (x, y) => _router.OnCursorMoved(x, y);
            _hooks.PanicPressed += () => _router.ReturnToPc();
            _hooks.Start();

            foreach (var w in configWarnings) _tray.Balloon("PhoneEdge config", w, ToolTipIcon.Warning);

            var installError = ScrcpyInstall.Validate(cfg.ScrcpyDir, ScrcpyInstall.RunVersion);
            if (installError is not null)
            {
                _log.Error("{Err}", installError);
                _tray.SetStatus("scrcpy problem - see log", TrayColor.Red);
                _tray.Balloon("PhoneEdge", installError, ToolTipIcon.Error);
                return;
            }
            StartSession();
        }
        catch
        {
            _hooks?.Dispose();
            _raw?.Dispose();
            _tray.Dispose();
            throw;
        }
    }

    private string _lastStatus = "Starting";

    private void StartSession()
    {
        var s = new PhoneSession(_cfg, _log);
        s.StateChanged += OnSessionState;
        s.Connected += (sink, info) => { _sink.Target = sink; _router.SetPhoneReady(info.Width, info.Height); };
        s.DisplayChanged += info => _router.UpdatePhoneBounds(info.Width, info.Height);
        s.Disconnected += () => { _router.SetPhoneUnavailable(); _sink.Target = null; };
        _session = s;
        s.Start();
    }

    private void OnSessionState(SessionState state, string status)
    {
        Ui(() =>
        {
            _lastStatus = status;
            switch (state)
            {
                case SessionState.Ready:
                    _tray.SetStatus(status, TrayColor.Green);
                    if (!_wasReady) _tray.Balloon("PhoneEdge", $"Phone connected: {status}", ToolTipIcon.Info);
                    _wasReady = true;
                    break;
                case SessionState.TearingDown:
                    if (_wasReady && !_exiting) _tray.Balloon("PhoneEdge", "Phone disconnected", ToolTipIcon.Warning);
                    _wasReady = false;
                    _tray.SetStatus(status, TrayColor.Grey);
                    break;
                default:
                    _tray.SetStatus(status, TrayColor.Grey);
                    break;
            }
            _lastState = state;
        });
    }

    private async Task RunDiagnosticsAsync()
    {
        if (_diagnosticsRunning) return;
        _diagnosticsRunning = true;
        try
        {
            _log.Information("Running diagnostics; stopping session first");
            if (_session is not null) { await _session.StopAsync(); _session.Dispose(); _session = null; }
            string report;
            try { report = await Diagnostics.RunAsync(_cfg, _log, CancellationToken.None); }
            catch (Exception ex) { report = "FAIL" + Environment.NewLine + ex; }
            File.WriteAllText(Path.Combine(Program.BaseDir, "phoneedge-diag.txt"), report);
            Ui(() => MessageBox.Show(report, "PhoneEdge diagnostics", MessageBoxButtons.OK, report.StartsWith("PASS") ? MessageBoxIcon.Information : MessageBoxIcon.Warning));
            if (!_exiting && _session is null) StartSession();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Diagnostics run failed");
            if (!_exiting && _session is null) StartSession();
        }
        finally { _diagnosticsRunning = false; }
    }

    private void OpenConfig()
    {
        if (!File.Exists(Program.ConfigPath))
        {
            File.WriteAllText(Program.ConfigPath, """
                {
                  "edge": "left",
                  "monitor": "primary",
                  "enterDwellMs": 1000,
                  "exitDwellMs": 1000,
                  "sensitivity": 1.0,
                  "forwardKeyboard": true,
                  "rightClickIsBack": true,
                  "panicHotkey": ["LControl", "RControl"],
                  "logLevel": "Info"
                }
                """);
        }
        OpenPath(Program.ConfigPath);
    }

    private static void OpenPath(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { /* no associated app */ }
    }

    private void Ui(Action a)
    {
        if (_tray.UiControl.IsDisposed) return;
        try
        {
            if (_tray.UiControl.InvokeRequired) _tray.UiControl.BeginInvoke(a); else a();
        }
        catch (ObjectDisposedException) { /* tray is being torn down */ }
        catch (InvalidOperationException) { /* tray is being torn down */ }
    }

    /// <summary>Last-resort release used by the unhandled-exception handlers.</summary>
    public void EmergencyRelease()
    {
        try { _router.ReturnToPc(); } catch { }
        try { _hooks.Swallow = false; } catch { }
    }

    private void Exit()
    {
        if (_exiting) return;
        _exiting = true;
        _log.Information("Exiting");
        _router.ReturnToPc();
        if (_session is not null)
        {
            var s = _session;
            try { Task.Run(() => s.StopAsync()).Wait(TimeSpan.FromSeconds(8)); }
            catch (Exception ex) { _log.Warning(ex, "Session stop failed"); }
        }
        _session?.Dispose();
        _hooks.Dispose();
        _raw.Dispose();
        _tray.Dispose();
        ExitThread();
    }
}
