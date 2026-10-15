using DroidEdge.Core.Input;

namespace DroidEdge.Win;

/// <summary>A set of virtual keys that must all be held simultaneously.</summary>
public sealed class PanicHotkey
{
    private readonly HashSet<int> _down = new();

    private PanicHotkey(int[] keys) => VirtualKeys = keys;

    public IReadOnlyList<int> VirtualKeys { get; }

    public static PanicHotkey Parse(IEnumerable<string> names, Action<string> warn)
    {
        var keys = new List<int>();
        foreach (var n in names)
        {
            if (KeyNames.Find(n) is { } key) keys.Add(key.VirtualKey);
            else warn($"Unknown panic hotkey key '{n}' (see the README for key names); if no valid keys remain, LControl+RControl is used");
        }
        if (keys.Count == 0) keys = [0xA2, 0xA3];
        return new PanicHotkey(keys.Distinct().ToArray());
    }

    /// <returns>true only on the key event that completes the combination (not on auto-repeat while it stays held).</returns>
    public bool OnKey(int vkCode, bool down)
    {
        if (!VirtualKeys.Contains(vkCode)) return false;
        bool wasComplete = _down.Count == VirtualKeys.Count;
        if (down) _down.Add(vkCode); else _down.Remove(vkCode);
        bool nowComplete = _down.Count == VirtualKeys.Count;
        return down && nowComplete && !wasComplete;
    }
}
