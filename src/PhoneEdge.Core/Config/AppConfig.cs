namespace PhoneEdge.Core.Config;

public sealed record AppConfig
{
    public string ScrcpyDir { get; init; } = @"C:\Users\you\AppData\Local\Programs\scrcpy";
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
    public string[] PanicHotkey { get; init; } = ["LControl", "RControl"];
    public string LogLevel { get; init; } = "Info";

    public static AppConfig Default { get; } = new();

    public bool Equals(AppConfig? other) =>
        other is not null &&
        ScrcpyDir == other.ScrcpyDir && DeviceSerial == other.DeviceSerial && Edge == other.Edge &&
        Monitor == other.Monitor && EnterDwellMs == other.EnterDwellMs && ExitDwellMs == other.ExitDwellMs &&
        Sensitivity == other.Sensitivity && ForwardKeyboard == other.ForwardKeyboard && RightClickIsBack == other.RightClickIsBack &&
        PanicHotkey.SequenceEqual(other.PanicHotkey) && LogLevel == other.LogLevel;

    public override int GetHashCode() => HashCode.Combine(ScrcpyDir, Edge, Monitor, EnterDwellMs, ExitDwellMs, Sensitivity, ForwardKeyboard, LogLevel);
}
