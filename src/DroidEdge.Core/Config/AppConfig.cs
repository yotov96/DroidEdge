namespace DroidEdge.Core.Config;

public sealed record AppConfig
{
    /// <summary>Folder with scrcpy.exe, adb.exe and scrcpy-server (an unzipped scrcpy release).</summary>
    public string ScrcpyDir { get; init; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "scrcpy");
    public string? DeviceSerial { get; init; }
    public Edge Edge { get; init; } = Edge.Left;
    /// <summary>"primary" or a zero-based monitor index as a string.</summary>
    public string Monitor { get; init; } = "primary";
    public int EnterDwellMs { get; init; } = 1000;
    public int ExitDwellMs { get; init; } = 1000;
    public double Sensitivity { get; init; } = 1.0;
    public bool ForwardKeyboard { get; init; } = true;
    /// <summary>Send right-click as Android Back instead of a secondary mouse button.</summary>
    public bool RightClickIsBack { get; init; } = true;
    /// <summary>Middle-click while on the phone turns the phone screen off and returns the mouse to the PC.</summary>
    public bool MiddleClickIsDone { get; init; } = true;
    /// <summary>Phone copies land in the Windows clipboard; the Windows clipboard is sent to the phone when crossing.</summary>
    public bool SyncClipboard { get; init; } = true;
    /// <summary>Open the phone desktop window (scrcpy virtual display) whenever the phone connects.</summary>
    public bool OpenDesktopOnConnect { get; init; } = true;
    /// <summary>Virtual display size and density in scrcpy's --new-display syntax: "WxH", "WxH/dpi", "/dpi", or "" for the phone's own.</summary>
    public string DesktopDisplay { get; init; } = "1920x1080";
    /// <summary>Resize the virtual display to follow the window (scrcpy --flex-display).</summary>
    public bool DesktopFlex { get; init; } = true;
    /// <summary>scrcpy --keyboard mode for the desktop window: "uhid" (follows the phone's layouts), "sdk" or "disabled".</summary>
    public string DesktopKeyboard { get; init; } = "uhid";
    /// <summary>scrcpy --mouse mode for the desktop window: "uhid" (captured, relative), "sdk" (plain window) or "disabled".</summary>
    public string DesktopMouse { get; init; } = "uhid";
    /// <summary>scrcpy --shortcut-mod for the desktop window: comma-separated lctrl, rctrl, lalt, ralt, lsuper, rsuper.</summary>
    public string DesktopShortcutMod { get; init; } = "rctrl";
    /// <summary>The tilde key taps the first shortcut key, which releases the captured mouse (with DesktopMouse "uhid").</summary>
    public bool DesktopTildeReleasesMouse { get; init; } = true;
    /// <summary>Keep the phone awake while the window is open (scrcpy --stay-awake), its own screen dark unless the mouse is on it.</summary>
    public bool DesktopKeepAwake { get; init; } = true;
    /// <summary>Closing the window moves its apps to the phone screen instead of closing them (scrcpy --no-vd-destroy-content).</summary>
    public bool DesktopKeepAppsOnClose { get; init; } = true;
    /// <summary>Use the patched scrcpy server for the window when it has been built (desktop windows on Android 17).</summary>
    public bool DesktopPatchedServer { get; init; } = true;
    /// <summary>Use the patched scrcpy client for the window when it has been built (keyboard grab while the mouse is captured).</summary>
    public bool DesktopPatchedClient { get; init; } = true;
    public string[] PanicHotkey { get; init; } = ["LControl", "RControl"];
    public string LogLevel { get; init; } = "Info";

    public static AppConfig Default { get; } = new();

    public bool Equals(AppConfig? other) =>
        other is not null &&
        ScrcpyDir == other.ScrcpyDir && DeviceSerial == other.DeviceSerial && Edge == other.Edge &&
        Monitor == other.Monitor && EnterDwellMs == other.EnterDwellMs && ExitDwellMs == other.ExitDwellMs &&
        Sensitivity == other.Sensitivity && ForwardKeyboard == other.ForwardKeyboard && RightClickIsBack == other.RightClickIsBack &&
        MiddleClickIsDone == other.MiddleClickIsDone && SyncClipboard == other.SyncClipboard &&
        OpenDesktopOnConnect == other.OpenDesktopOnConnect && DesktopDisplay == other.DesktopDisplay && DesktopFlex == other.DesktopFlex &&
        DesktopKeyboard == other.DesktopKeyboard && DesktopMouse == other.DesktopMouse && DesktopShortcutMod == other.DesktopShortcutMod &&
        DesktopTildeReleasesMouse == other.DesktopTildeReleasesMouse && DesktopKeepAwake == other.DesktopKeepAwake &&
        DesktopKeepAppsOnClose == other.DesktopKeepAppsOnClose && DesktopPatchedServer == other.DesktopPatchedServer &&
        DesktopPatchedClient == other.DesktopPatchedClient &&
        PanicHotkey.SequenceEqual(other.PanicHotkey) && LogLevel == other.LogLevel;

    public override int GetHashCode() => HashCode.Combine(ScrcpyDir, Edge, Monitor, EnterDwellMs, ExitDwellMs, Sensitivity, ForwardKeyboard, LogLevel);
}
