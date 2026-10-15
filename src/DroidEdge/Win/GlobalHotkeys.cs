using System.Runtime.InteropServices;
using DroidEdge.Core.Input;
using DroidEdge.Native;
using Serilog;

namespace DroidEdge.Win;

/// <summary>
/// System-wide hotkeys (RegisterHotKey) on a message-only window. Create and use on the UI thread: the actions run there.
/// Keys the low-level hook blocks, such as while the mouse is on the phone, never reach Windows' hotkey handling.
/// </summary>
public sealed class GlobalHotkeys : NativeWindow, IDisposable
{
    private readonly ILogger _log;
    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId = 1;

    public GlobalHotkeys(ILogger log)
    {
        _log = log;
        CreateHandle(new CreateParams { Parent = User32.HWND_MESSAGE, Caption = "DroidEdgeHotkeys" });
    }

    /// <param name="setting">The config setting the hotkey comes from, for messages.</param>
    /// <param name="text">The hotkey as in the config, e.g. "Ctrl+Alt+D"; empty registers nothing.</param>
    /// <returns>Null when registered or empty, otherwise why it could not be registered.</returns>
    public string? Register(string setting, string text, Action action)
    {
        if (text.Length == 0) return null;
        if (!Hotkey.TryParse(text, out Hotkey? hotkey, out string error)) return $"'{setting}': {error}";
        int id = _nextId++;
        if (!User32.RegisterHotKey(Handle, id, (uint)hotkey!.Modifiers | User32.MOD_NOREPEAT, (uint)hotkey.Key.VirtualKey))
        {
            int code = Marshal.GetLastWin32Error();
            return $"'{setting}': {hotkey} could not be registered (error {code}); another program probably uses it";
        }
        _actions[id] = action;
        _log.Information("Hotkey {Hotkey} registered for {Setting}", hotkey, setting);
        return null;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == User32.WM_HOTKEY && _actions.TryGetValue((int)m.WParam, out Action? action))
        {
            try { action(); }
            catch (Exception ex) { _log.Error(ex, "Hotkey action failed"); }
            return;
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        foreach (int id in _actions.Keys) User32.UnregisterHotKey(Handle, id);
        _actions.Clear();
        DestroyHandle();
    }
}
