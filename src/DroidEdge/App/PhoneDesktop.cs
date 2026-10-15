using System.Diagnostics;
using DroidEdge.Core.Config;
using DroidEdge.Native;
using DroidEdge.Win;
using Serilog;

namespace DroidEdge.App;

/// <summary>
/// The phone desktop window: a separate scrcpy.exe showing a virtual display created on the phone. It runs its own
/// scrcpy server, so minimizing or closing it never affects the mouse connection. Call only on the UI thread.
/// </summary>
public sealed class PhoneDesktop : IDisposable
{
    public const string WindowTitle = "Phone desktop";

    // scrcpy's server with the Android 17 desktop patch, built by scrcpy\build-server.ps1. Without it scrcpy uses its own
    // server, whose virtual display cannot host desktop windows.
    private static readonly string DesktopServerPath = Path.Combine(AppContext.BaseDirectory, "scrcpy-server-desktop");

    // scrcpy's client with the keyboard grab patch, built by scrcpy\build-client.ps1: while it holds the mouse, other
    // programs' global shortcuts (GlazeWM, launchers) reach the phone. Without it the installed scrcpy.exe is used.
    private static readonly string DesktopClientPath = Path.Combine(AppContext.BaseDirectory, "scrcpy-desktop", "scrcpy.exe");

    private readonly string _scrcpyExe;
    private readonly string _adbExe;
    private readonly ILogger _log;
    private Process? _process;
    private AppConfig? _openedWith; // the settings of the running window; the config file may have changed since

    public PhoneDesktop(string scrcpyDir, ILogger log)
    {
        _scrcpyExe = Path.Combine(scrcpyDir, "scrcpy.exe");
        _adbExe = Path.Combine(scrcpyDir, "adb.exe");
        _log = log;
    }

    public bool IsOpen => _process is { HasExited: false };

    /// <summary>Process id of the window's scrcpy.exe, or 0 when it is not open.</summary>
    public int ProcessId => IsOpen ? _process!.Id : 0;

    /// <summary>The window is open and keeps the phone awake (desktopKeepAwake).</summary>
    public bool KeepsPhoneAwake => IsOpen && _openedWith!.DesktopKeepAwake;

    /// <summary>
    /// The release key that taps the window's first --shortcut-mod key to release its captured mouse, or null when the
    /// window is closed, does not capture the mouse, or desktopReleaseKey is empty.
    /// </summary>
    public ReleaseKeyBinding? ReleaseKey =>
        IsOpen && _openedWith!.DesktopMouse == "uhid" && _openedWith.DesktopReleaseKey.Length > 0
            ? ReleaseKeyBinding.Create(_openedWith.DesktopReleaseKey, _openedWith.DesktopShortcutMod.Split(',')[0])
            : null;

    /// <summary>The scrcpy process exited (window closed, phone unplugged). Raised on a thread-pool thread.</summary>
    public event Action? Closed;

    public static List<string> BuildArguments(string serial, AppConfig cfg)
    {
        List<string> args = ["-s", serial, cfg.DesktopDisplay.Length == 0 ? "--new-display" : $"--new-display={cfg.DesktopDisplay}"];
        if (cfg.DesktopFlex) args.Add("--flex-display");
        // Using the virtual display does not count as activity for the phone's own screen, so without --stay-awake
        // the phone sleeps and locks after its screen timeout. scrcpy restores the setting when the window closes.
        if (cfg.DesktopKeepAwake) args.Add("--stay-awake");
        // uhid keyboard: follows the layout set on the phone for it. scrcpy's default mode maps letters with Android's
        // US layout, and its text injection drops characters that layout cannot type (Cyrillic, for instance).
        // uhid mouse: relative movement, captured by the window, so apps that grab the pointer (Moonlight) get a single
        // pointer instead of an absolute one trailing the Windows cursor.
        args.AddRange([$"--keyboard={cfg.DesktopKeyboard}", $"--mouse={cfg.DesktopMouse}", $"--shortcut-mod={cfg.DesktopShortcutMod}"]);
        // Clipboard is handled by our own connection
        args.AddRange(["--no-audio", "--no-clipboard-autosync"]);
        if (cfg.DesktopKeepAppsOnClose) args.Add("--no-vd-destroy-content");
        args.Add($"--window-title={WindowTitle}");
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

        bool patchedClient = cfg.DesktopPatchedClient && File.Exists(DesktopClientPath);
        ProcessStartInfo psi = new(patchedClient ? DesktopClientPath : _scrcpyExe)
        {
            UseShellExecute = false,
            CreateNoWindow = true, // scrcpy.exe is a console program; only its video window should appear
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in BuildArguments(serial, cfg)) psi.ArgumentList.Add(a);
        bool patchedServer = cfg.DesktopPatchedServer && File.Exists(DesktopServerPath);
        if (patchedServer) psi.Environment["SCRCPY_SERVER_PATH"] = DesktopServerPath;
        // The patched client has no adb.exe next to it; use the installed one, so only one adb server version runs
        if (patchedClient) psi.Environment["ADB"] = _adbExe;

        var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) _log.Information("[desktop] {Line}", e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) _log.Information("[desktop] {Line}", e.Data); };
        p.Exited += (_, _) =>
        {
            _log.Information("Phone desktop closed");
            Closed?.Invoke();
        };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        _process = p;
        _openedWith = cfg;
        _log.Information("Phone desktop opened: {Client} {Args} ({Server})", psi.FileName, string.Join(' ', psi.ArgumentList),
            patchedServer ? $"server {DesktopServerPath}" : "stock server");
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
