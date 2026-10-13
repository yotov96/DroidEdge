using System.Text.Json;
using System.Text.Json.Serialization;

namespace DroidEdge.Core.Config;

public static class ConfigLoader
{
    private sealed class Dto
    {
        public string? ScrcpyDir { get; set; }
        public string? DeviceSerial { get; set; }
        public string? Edge { get; set; }
        public string? Monitor { get; set; }
        public int? EnterDwellMs { get; set; }
        public int? ExitDwellMs { get; set; }
        public double? Sensitivity { get; set; }
        public bool? ForwardKeyboard { get; set; }
        public bool? RightClickIsBack { get; set; }
        public bool? MiddleClickIsDone { get; set; }
        public bool? SyncClipboard { get; set; }
        public bool? OpenDesktopOnConnect { get; set; }
        public string? DesktopDisplay { get; set; }
        public bool? DesktopFlex { get; set; }
        public string? DesktopKeyboard { get; set; }
        public string? DesktopMouse { get; set; }
        public string? DesktopShortcutMod { get; set; }
        public bool? DesktopTildeReleasesMouse { get; set; }
        public bool? DesktopKeepAwake { get; set; }
        public bool? DesktopKeepAppsOnClose { get; set; }
        public bool? DesktopPatchedServer { get; set; }
        public bool? DesktopPatchedClient { get; set; }
        public string[]? PanicHotkey { get; set; }
        public string? LogLevel { get; set; }
    }

    private static readonly System.Text.RegularExpressions.Regex DesktopDisplayPattern = new(@"^(\d{2,5}x\d{2,5})?(/\d{2,4})?$");

    // scrcpy's --keyboard and --mouse modes that make sense for the desktop window ("aoa" needs scrcpy's OTG mode)
    private static readonly string[] DesktopInputModes = ["uhid", "sdk", "disabled"];

    /// <summary>Keys scrcpy accepts in --shortcut-mod.</summary>
    public static readonly string[] ShortcutModKeys = ["lctrl", "rctrl", "lalt", "ralt", "lsuper", "rsuper"];

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public static AppConfig Load(string path, Action<string> warn)
    {
        if (!File.Exists(path)) return AppConfig.Default;
        string json;
        try { json = File.ReadAllText(path); }
        catch (Exception ex) { warn($"Could not read config '{path}': {ex.Message}"); return AppConfig.Default; }
        return Parse(json, warn);
    }

    public static AppConfig Parse(string json, Action<string> warn)
    {
        Dto? dto;
        try { dto = string.IsNullOrWhiteSpace(json) ? new Dto() : JsonSerializer.Deserialize<Dto>(json, Options); }
        catch (JsonException ex) { warn($"Config is not valid JSON, using defaults: {ex.Message}"); return AppConfig.Default; }
        dto ??= new Dto();
        var d = AppConfig.Default;

        Edge edge = d.Edge;
        if (dto.Edge is not null)
        {
            if (Enum.TryParse<Edge>(dto.Edge, ignoreCase: true, out var e)) edge = e;
            else warn($"Config 'edge' must be left|right|top|bottom, got '{dto.Edge}'; using {d.Edge}");
        }

        string monitor = d.Monitor;
        if (dto.Monitor is not null)
        {
            if (dto.Monitor.Equals("primary", StringComparison.OrdinalIgnoreCase) || (int.TryParse(dto.Monitor, out var idx) && idx >= 0)) monitor = dto.Monitor.ToLowerInvariant();
            else warn($"Config 'monitor' must be 'primary' or a non-negative index, got '{dto.Monitor}'; using primary");
        }

        int enter = PositiveOrDefault(dto.EnterDwellMs, d.EnterDwellMs, "enterDwellMs", warn);
        int exit = PositiveOrDefault(dto.ExitDwellMs, d.ExitDwellMs, "exitDwellMs", warn);

        double sens = d.Sensitivity;
        if (dto.Sensitivity is double s)
        {
            if (s > 0 && s <= 20 && double.IsFinite(s)) sens = s;
            else warn($"Config 'sensitivity' must be in (0, 20], got {s}; using {d.Sensitivity}");
        }

        string desktopDisplay = d.DesktopDisplay;
        if (dto.DesktopDisplay is not null)
        {
            if (DesktopDisplayPattern.IsMatch(dto.DesktopDisplay)) desktopDisplay = dto.DesktopDisplay;
            else warn($"Config 'desktopDisplay' must look like 1920x1080, 1920x1080/240, /240 or be empty, got '{dto.DesktopDisplay}'; using {d.DesktopDisplay}");
        }

        string desktopKeyboard = OneOf(dto.DesktopKeyboard, DesktopInputModes, d.DesktopKeyboard, "desktopKeyboard", warn);
        string desktopMouse = OneOf(dto.DesktopMouse, DesktopInputModes, d.DesktopMouse, "desktopMouse", warn);

        string shortcutMod = d.DesktopShortcutMod;
        if (dto.DesktopShortcutMod is not null)
        {
            string[] keys = dto.DesktopShortcutMod.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.ToLowerInvariant()).ToArray();
            if (keys.Length > 0 && keys.All(ShortcutModKeys.Contains)) shortcutMod = string.Join(',', keys);
            else warn($"Config 'desktopShortcutMod' must be a comma-separated list of {string.Join(", ", ShortcutModKeys)}, got '{dto.DesktopShortcutMod}'; using {d.DesktopShortcutMod}");
        }

        string[] hotkey = d.PanicHotkey;
        if (dto.PanicHotkey is not null)
        {
            if (dto.PanicHotkey.Length >= 1 && dto.PanicHotkey.All(k => !string.IsNullOrWhiteSpace(k))) hotkey = dto.PanicHotkey;
            else warn("Config 'panicHotkey' must be a non-empty list of key names; using default");
        }

        return new AppConfig
        {
            ScrcpyDir = string.IsNullOrWhiteSpace(dto.ScrcpyDir) ? d.ScrcpyDir : dto.ScrcpyDir,
            DeviceSerial = string.IsNullOrWhiteSpace(dto.DeviceSerial) ? null : dto.DeviceSerial,
            Edge = edge,
            Monitor = monitor,
            EnterDwellMs = enter,
            ExitDwellMs = exit,
            Sensitivity = sens,
            ForwardKeyboard = dto.ForwardKeyboard ?? d.ForwardKeyboard,
            RightClickIsBack = dto.RightClickIsBack ?? d.RightClickIsBack,
            MiddleClickIsDone = dto.MiddleClickIsDone ?? d.MiddleClickIsDone,
            SyncClipboard = dto.SyncClipboard ?? d.SyncClipboard,
            OpenDesktopOnConnect = dto.OpenDesktopOnConnect ?? d.OpenDesktopOnConnect,
            DesktopDisplay = desktopDisplay,
            DesktopFlex = dto.DesktopFlex ?? d.DesktopFlex,
            DesktopKeyboard = desktopKeyboard,
            DesktopMouse = desktopMouse,
            DesktopShortcutMod = shortcutMod,
            DesktopTildeReleasesMouse = dto.DesktopTildeReleasesMouse ?? d.DesktopTildeReleasesMouse,
            DesktopKeepAwake = dto.DesktopKeepAwake ?? d.DesktopKeepAwake,
            DesktopKeepAppsOnClose = dto.DesktopKeepAppsOnClose ?? d.DesktopKeepAppsOnClose,
            DesktopPatchedServer = dto.DesktopPatchedServer ?? d.DesktopPatchedServer,
            DesktopPatchedClient = dto.DesktopPatchedClient ?? d.DesktopPatchedClient,
            PanicHotkey = hotkey,
            LogLevel = string.IsNullOrWhiteSpace(dto.LogLevel) ? d.LogLevel : dto.LogLevel,
        };
    }

    private static string OneOf(string? value, string[] allowed, string fallback, string name, Action<string> warn)
    {
        if (value is null) return fallback;
        string normalized = value.Trim().ToLowerInvariant();
        if (allowed.Contains(normalized)) return normalized;
        warn($"Config '{name}' must be one of {string.Join(", ", allowed)}, got '{value}'; using {fallback}");
        return fallback;
    }

    private static int PositiveOrDefault(int? value, int fallback, string name, Action<string> warn)
    {
        if (value is null) return fallback;
        if (value.Value >= 0 && value.Value <= 60_000) return value.Value;
        warn($"Config '{name}' must be 0..60000, got {value}; using {fallback}");
        return fallback;
    }
}
