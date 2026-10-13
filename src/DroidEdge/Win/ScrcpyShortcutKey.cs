namespace DroidEdge.Win;

/// <summary>A key scrcpy accepts in --shortcut-mod, as Windows sends it.</summary>
/// <param name="Extended">The key's scan code has the E0 prefix, which is how SDL tells right-hand keys from left-hand ones.</param>
public sealed record ScrcpyShortcutKey(ushort VirtualKey, ushort ScanCode, bool Extended)
{
    /// <returns>The key for a --shortcut-mod name (lctrl, rctrl, lalt, ralt, lsuper, rsuper), or null for any other name.</returns>
    public static ScrcpyShortcutKey? FromName(string name) => name switch
    {
        "lctrl" => new(0xA2, 0x1D, false),
        "rctrl" => new(0xA3, 0x1D, true),
        "lalt" => new(0xA4, 0x38, false),
        "ralt" => new(0xA5, 0x38, true),
        "lsuper" => new(0x5B, 0x5B, true),
        "rsuper" => new(0x5C, 0x5C, true),
        _ => null,
    };
}
