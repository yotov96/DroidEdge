using Serilog;

namespace PhoneEdge.Phone;

/// <summary>Disables pointer acceleration on the phone (so HID counts map linearly to pixels) and restores the previous values.</summary>
public sealed class AndroidSettings
{
    private const string SpeedKey = "pointer_speed";
    private const string AccelKey = "mouse_pointer_acceleration_enabled";
    private readonly Adb _adb;
    private readonly ILogger _log;
    private string? _origSpeed, _origAccel;
    private bool _applied;

    public AndroidSettings(Adb adb, ILogger log)
    {
        _adb = adb;
        _log = log;
    }

    public async Task ApplyPointerSettingsAsync(CancellationToken ct)
    {
        if (_applied) return;
        _origSpeed = AdbParsers.ParseSettingValue((await _adb.ShellAsync(ct, "settings", "get", "system", SpeedKey)).StdOut);
        _origAccel = AdbParsers.ParseSettingValue((await _adb.ShellAsync(ct, "settings", "get", "system", AccelKey)).StdOut);
        _log.Information("Saved pointer settings: {Speed} / {Accel}", _origSpeed ?? "unset", _origAccel ?? "unset");
        await _adb.ShellAsync(ct, "settings", "put", "system", SpeedKey, "0");
        await _adb.ShellAsync(ct, "settings", "put", "system", AccelKey, "0");
        _applied = true;
    }

    public async Task RestoreAsync(CancellationToken ct)
    {
        if (!_applied) return;
        _applied = false;
        try
        {
            await RestoreOne(SpeedKey, _origSpeed, ct);
            await RestoreOne(AccelKey, _origAccel, ct);
            _log.Information("Restored pointer settings");
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Could not restore pointer settings");
        }
    }

    private async Task RestoreOne(string key, string? value, CancellationToken ct)
    {
        if (value is null) await _adb.ShellAsync(ct, "settings", "delete", "system", key);
        else await _adb.ShellAsync(ct, "settings", "put", "system", key, value);
    }

    public async Task<DisplayInfo> GetDisplayInfoAsync(CancellationToken ct)
    {
        var dump = await _adb.RunAsync(TimeSpan.FromSeconds(10), ct, "shell", "dumpsys", "input");
        var vp = dump.Ok ? AdbParsers.ParseViewport(dump.StdOut) : null;
        if (vp is not null) return vp;

        var wm = await _adb.ShellAsync(ct, "wm", "size");
        var size = AdbParsers.ParseWmSize(wm.StdOut);
        if (size is null) throw new InvalidOperationException("Could not determine display size from 'dumpsys input' or 'wm size'");
        _log.Warning("Viewport not found in dumpsys input; assuming rotation 0 with size {W}x{H}", size.Value.w, size.Value.h);
        return new DisplayInfo(size.Value.w, size.Value.h, 0);
    }
}
