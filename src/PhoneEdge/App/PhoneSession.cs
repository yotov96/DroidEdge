using PhoneEdge.Core.Config;
using PhoneEdge.Core.Input;
using PhoneEdge.Phone;
using Serilog;

namespace PhoneEdge.App;

public enum SessionState { WaitingForDevice, Bootstrapping, Ready, TearingDown, Stopped }

/// <summary>Finds the phone, bootstraps the scrcpy server, keeps the control channel alive, reconnects with backoff.</summary>
public sealed class PhoneSession : IDisposable
{
    private static readonly TimeSpan DevicePoll = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RotationPoll = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StableAfter = TimeSpan.FromSeconds(30);

    private readonly AppConfig _cfg;
    private readonly ILogger _log;
    private readonly CancellationTokenSource _cts = new();
    private readonly string _adbPath;
    private readonly string _jarPath;
    private Task? _loop;

    public PhoneSession(AppConfig cfg, ILogger log)
    {
        _cfg = cfg;
        _log = log;
        _adbPath = Path.Combine(cfg.ScrcpyDir, "adb.exe");
        _jarPath = Path.Combine(cfg.ScrcpyDir, "scrcpy-server");
    }

    public SessionState State { get; private set; } = SessionState.Stopped;
    public event Action<SessionState, string>? StateChanged;
    public event Action<IReportSink, DisplayInfo>? Connected;
    public event Action<DisplayInfo>? DisplayChanged;
    public event Action? Disconnected;

    public void Start()
    {
        if (_loop is not null) throw new InvalidOperationException("already started");
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        _cts.Cancel();
        if (_loop is not null)
        {
            try { await _loop.WaitAsync(TimeSpan.FromSeconds(8)); }
            catch (Exception ex) { _log.Warning(ex, "Session loop did not stop cleanly"); }
        }
    }

    private void SetState(SessionState s, string status)
    {
        State = s;
        _log.Information("Session: {State} - {Status}", s, status);
        Raise(() => StateChanged?.Invoke(s, status), nameof(StateChanged));
    }

    private void Raise(Action handler, string name)
    {
        try { handler(); }
        catch (Exception ex) { _log.Error(ex, "{Handler} subscriber threw", name); }
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        var backoff = TimeSpan.FromSeconds(1);
        while (!ct.IsCancellationRequested)
        {
            AdbDevice? device;
            try
            {
                SetState(SessionState.WaitingForDevice, "Waiting for phone");
                device = await WaitForDeviceAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            if (device is null) break;

            var adb = new Adb(_adbPath, device.Serial, _log);
            var settings = new AndroidSettings(adb, _log);
            ControlChannel? channel = null;
            var readyAt = DateTime.MaxValue;
            try
            {
                SetState(SessionState.Bootstrapping, $"Connecting to {device.Serial}");
                var conn = await ServerBootstrap.StartAsync(adb, _jarPath, _log, ct);
                channel = new ControlChannel(conn, _log);
                var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                channel.ConnectionLost += _ => lost.TrySetResult();
                channel.Start();
                channel.CreateDevices();
                await settings.ApplyPointerSettingsAsync(ct);
                var info = await settings.GetDisplayInfoAsync(ct);

                readyAt = DateTime.UtcNow;
                SetState(SessionState.Ready, $"{conn.DeviceName} ({info.Width}x{info.Height})");
                Raise(() => Connected?.Invoke(channel, info), nameof(Connected));

                while (!ct.IsCancellationRequested && !lost.Task.IsCompleted)
                {
                    await Task.WhenAny(lost.Task, Task.Delay(RotationPoll, ct));
                    if (lost.Task.IsCompleted || ct.IsCancellationRequested) break;
                    DisplayInfo next;
                    try
                    {
                        var poll = settings.GetDisplayInfoAsync(ct);
                        var finished = await Task.WhenAny(lost.Task, poll);
                        if (finished == lost.Task) break;
                        next = await poll;
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex) { _log.Warning(ex, "Display poll failed; treating as disconnect"); break; }
                    if (next != info)
                    {
                        info = next;
                        _log.Information("Display changed: {W}x{H} rotation {R}", info.Width, info.Height, info.Rotation);
                        Raise(() => DisplayChanged?.Invoke(info), nameof(DisplayChanged));
                    }
                }
            }
            catch (OperationCanceledException) { /* stopping */ }
            catch (Exception ex)
            {
                _log.Error(ex, "Session failed");
            }
            finally
            {
                SetState(SessionState.TearingDown, "Disconnecting");
                Raise(() => Disconnected?.Invoke(), nameof(Disconnected));
                if (channel is not null)
                {
                    if (channel.IsAlive) channel.DestroyDevices();
                    channel.Dispose(TimeSpan.FromSeconds(1));
                }
                using (var restoreCts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    try { await settings.RestoreAsync(restoreCts.Token); }
                    catch (Exception ex) { _log.Warning(ex, "Restore failed or timed out"); }
                }
            }

            if (ct.IsCancellationRequested) break;
            if (DateTime.UtcNow - readyAt > StableAfter) backoff = TimeSpan.FromSeconds(1);
            try { await Task.Delay(backoff, ct); } catch (OperationCanceledException) { break; }
            backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, MaxBackoff.Ticks));
        }
        SetState(SessionState.Stopped, "Stopped");
    }

    private async Task<AdbDevice?> WaitForDeviceAsync(CancellationToken ct)
    {
        var probe = new Adb(_adbPath, null, _log);
        bool warnedMultiple = false;
        while (!ct.IsCancellationRequested)
        {
            List<AdbDevice> devices;
            try { devices = AdbParsers.ParseDevices((await probe.RunAsync(ct, "devices")).StdOut); }
            catch (OperationCanceledException) { return null; }
            catch (Exception ex) { _log.Warning(ex, "adb devices failed"); devices = []; }

            var candidates = devices.Where(d => d.State == "device" && (_cfg.DeviceSerial is null || d.Serial == _cfg.DeviceSerial)).ToList();
            if (candidates.Count > 1 && !warnedMultiple)
            {
                _log.Warning("Multiple devices attached ({List}); using {Serial}. Set deviceSerial in phoneedge.json to choose.", string.Join(", ", candidates.Select(c => c.Serial)), candidates[0].Serial);
                warnedMultiple = true;
            }
            if (candidates.Count > 0) return candidates[0];
            if (devices.Any(d => d.State == "unauthorized")) _log.Warning("Phone is attached but USB debugging is not authorized; accept the prompt on the phone");
            await Task.Delay(DevicePoll, ct);
        }
        return null;
    }

    public void Dispose()
    {
        // Cancel only; the loop may still be draining. StopAsync waits for it; the CTS is left to the GC.
        _cts.Cancel();
    }
}
