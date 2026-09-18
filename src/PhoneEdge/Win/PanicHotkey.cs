namespace PhoneEdge.Win;

/// <summary>A set of virtual keys that must all be held simultaneously.</summary>
public sealed class PanicHotkey
{
    private static readonly Dictionary<string, int> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LControl"] = 0xA2, ["RControl"] = 0xA3,
        ["LShift"] = 0xA0, ["RShift"] = 0xA1,
        ["LAlt"] = 0xA4, ["RAlt"] = 0xA5,
        ["LWin"] = 0x5B, ["RWin"] = 0x5C,
        ["Escape"] = 0x1B, ["Pause"] = 0x13, ["ScrollLock"] = 0x91,
        ["F12"] = 0x7B,
    };

    private readonly HashSet<int> _down = new();

    private PanicHotkey(int[] keys) => VirtualKeys = keys;

    public IReadOnlyList<int> VirtualKeys { get; }

    public static PanicHotkey Parse(IEnumerable<string> names, Action<string> warn)
    {
        var keys = new List<int>();
        foreach (var n in names)
        {
            if (Names.TryGetValue(n.Trim(), out var vk)) keys.Add(vk);
            else warn($"Unknown panic hotkey key '{n}' (known: {string.Join(", ", Names.Keys)}); if no valid keys remain, LControl+RControl is used");
        }
        if (keys.Count == 0) keys = [0xA2, 0xA3];
        return new PanicHotkey(keys.Distinct().ToArray());
    }

    /// <returns>true when this key event completes the combination.</returns>
    public bool OnKey(int vkCode, bool down)
    {
        if (!VirtualKeys.Contains(vkCode)) return false;
        if (down) _down.Add(vkCode); else _down.Remove(vkCode);
        return down && _down.Count == VirtualKeys.Count;
    }
}
