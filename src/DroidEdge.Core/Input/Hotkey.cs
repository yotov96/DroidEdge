namespace DroidEdge.Core.Input;

/// <summary>Modifier keys of a hotkey; the values are Windows' RegisterHotKey MOD_* flags.</summary>
[Flags]
public enum HotkeyModifiers { None = 0, Alt = 1, Ctrl = 2, Shift = 4, Win = 8 }

/// <summary>A global hotkey such as "Ctrl+Alt+D": modifiers and one key from <see cref="KeyNames"/>.</summary>
public sealed record Hotkey(HotkeyModifiers Modifiers, KeyInfo Key)
{
    private static readonly Dictionary<string, HotkeyModifiers> ModifierNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ctrl"] = HotkeyModifiers.Ctrl, ["Control"] = HotkeyModifiers.Ctrl,
        ["Alt"] = HotkeyModifiers.Alt,
        ["Shift"] = HotkeyModifiers.Shift,
        ["Win"] = HotkeyModifiers.Win,
    };

    public static bool TryParse(string text, out Hotkey? hotkey, out string error)
    {
        hotkey = null;
        string[] parts = text.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Any(p => p.Length == 0))
        {
            error = $"'{text}' is not a hotkey like Ctrl+Alt+D";
            return false;
        }
        HotkeyModifiers modifiers = HotkeyModifiers.None;
        foreach (string part in parts[..^1])
        {
            if (!ModifierNames.TryGetValue(part, out HotkeyModifiers modifier))
            {
                error = $"'{part}' in '{text}' is not a modifier (Ctrl, Alt, Shift, Win)";
                return false;
            }
            modifiers |= modifier;
        }
        KeyInfo? key = KeyNames.Find(parts[^1]);
        if (key is null || KeyNames.IsModifier(key))
        {
            error = $"'{parts[^1]}' in '{text}' is not a key name usable in a hotkey";
            return false;
        }
        hotkey = new Hotkey(modifiers, key);
        error = "";
        return true;
    }

    /// <summary>The canonical spelling, e.g. "Ctrl+Alt+D".</summary>
    public override string ToString()
    {
        List<string> parts = [];
        if (Modifiers.HasFlag(HotkeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(Key.Name);
        return string.Join('+', parts);
    }
}
