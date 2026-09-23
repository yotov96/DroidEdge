using System.Diagnostics;
using PhoneEdge.Core.Config;
using PhoneEdge.Native;
using Serilog;

namespace PhoneEdge.App;

/// <summary>
/// The phone desktop window: a separate scrcpy.exe showing a virtual display created on the phone. It runs its own
/// scrcpy server, so minimizing or closing it never affects the mouse connection. Call only on the UI thread.
/// </summary>
public sealed class PhoneDesktop : IDisposable
{
    public const string WindowTitle = "Phone desktop";

    private readonly string _scrcpyExe;
    private readonly ILogger _log;
    private Process? _process;

    public PhoneDesktop(string scrcpyDir, ILogger log)
    {
        _scrcpyExe = Path.Combine(scrcpyDir, "scrcpy.exe");
        _log = log;
    }

    public bool IsOpen => _process is { HasExited: false };

    public static List<string> BuildArguments(string serial, AppConfig cfg)
    {
        var args = new List<string> { "-s", serial, cfg.DesktopDisplay.Length == 0 ? "--new-display" : $"--new-display={cfg.DesktopDisplay}" };
        if (cfg.DesktopFlex) args.Add("--flex-display");
        // Clipboard is handled by our own connection; closing the window moves its apps to the phone screen instead of killing them.
        args.AddRange(["--no-audio", "--no-clipboard-autosync", "--no-vd-destroy-content", $"--window-title={WindowTitle}"]);
        return args;
    }

    /// <summary>Starts the window; if it is already open, brings it to the front when <paramref name="bringToFront"/> is set.</summary>
    public void Open(string serial, AppConfig cfg, bool bringToFront)
    {
        if (IsOpen)
        {
            if (bringToFront) BringToFront();
            return;
        }
        _process?.Dispose();

        var psi = new ProcessStartInfo(_scrcpyExe)
        {
            UseShellExecute = false,
            CreateNoWindow = true, // scrcpy.exe is a console program; only its video window should appear
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in BuildArguments(serial, cfg)) psi.ArgumentList.Add(a);

        var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) _log.Information("[desktop] {Line}", e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) _log.Information("[desktop] {Line}", e.Data); };
        p.Exited += (_, _) => _log.Information("Phone desktop closed");
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        _process = p;
        _log.Information("Phone desktop opened: scrcpy {Args}", string.Join(' ', psi.ArgumentList));
    }

    private void BringToFront()
    {
        _process!.Refresh();
        var hwnd = _process.MainWindowHandle;
        if (hwnd == IntPtr.Zero) return;
        if (User32.IsIconic(hwnd)) User32.ShowWindow(hwnd, User32.SW_RESTORE);
        User32.SetForegroundWindow(hwnd);
    }

    public void Close()
    {
        if (_process is null) return;
        try
        {
            // Ask the window to close so scrcpy shuts its server down cleanly; kill only if it does not.
            if (!_process.HasExited && !(_process.CloseMainWindow() && _process.WaitForExit(2000)))
                _process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "Closing the phone desktop failed");
        }
        _process.Dispose();
        _process = null;
    }

    public void Dispose() => Close();
}
