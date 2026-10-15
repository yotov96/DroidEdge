namespace DroidEdge.Core.Input;

/// <summary>A key as it can be named in the config.</summary>
/// <param name="VirtualKey">Windows virtual-key code.</param>
/// <param name="ScanCode">Scan code (set 1), which identifies the physical key whatever the keyboard layout.</param>
/// <param name="Extended">The scan code has the E0 prefix.</param>
public sealed record KeyInfo(string Name, int VirtualKey, ushort ScanCode, bool Extended);

/// <summary>Key names accepted in the config. Letters, digits and punctuation are named after their place on a US layout.</summary>
public static class KeyNames
{
    public static IReadOnlyList<KeyInfo> All { get; } = Build();

    private static readonly Dictionary<string, KeyInfo> ByName = All.ToDictionary(k => k.Name, StringComparer.OrdinalIgnoreCase);

    /// <returns>The key with that name (case-insensitive), or null.</returns>
    public static KeyInfo? Find(string name) => ByName.GetValueOrDefault(name.Trim());

    /// <summary>Ctrl, Shift, Alt and Windows keys, which are modifiers in a hotkey rather than its key.</summary>
    public static bool IsModifier(KeyInfo key) => key.Name is "LControl" or "RControl" or "LShift" or "RShift" or "LAlt" or "RAlt" or "LWin" or "RWin";

    private static List<KeyInfo> Build()
    {
        List<KeyInfo> keys = [];
        void Add(string name, int vk, ushort scan, bool extended = false) => keys.Add(new KeyInfo(name, vk, scan, extended));

        void AddRow(string letters, ushort firstScan)
        {
            for (int i = 0; i < letters.Length; i++) Add(letters[i].ToString(), letters[i], (ushort)(firstScan + i));
        }
        AddRow("QWERTYUIOP", 0x10);
        AddRow("ASDFGHJKL", 0x1E);
        AddRow("ZXCVBNM", 0x2C);
        for (int d = 1; d <= 9; d++) Add(d.ToString(), '0' + d, (ushort)(0x01 + d));
        Add("0", '0', 0x0B);

        for (int i = 0; i < 10; i++) Add($"F{i + 1}", 0x70 + i, (ushort)(0x3B + i));
        Add("F11", 0x7A, 0x57);
        Add("F12", 0x7B, 0x58);
        ushort[] f13To24 = [0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6A, 0x6B, 0x6C, 0x6D, 0x6E, 0x76];
        for (int i = 0; i < f13To24.Length; i++) Add($"F{13 + i}", 0x7C + i, f13To24[i]);

        Add("Escape", 0x1B, 0x01);
        Add("Tilde", 0xC0, 0x29); // the key left of 1
        Add("Minus", 0xBD, 0x0C);
        Add("Equals", 0xBB, 0x0D);
        Add("Backspace", 0x08, 0x0E);
        Add("Tab", 0x09, 0x0F);
        Add("LeftBracket", 0xDB, 0x1A);
        Add("RightBracket", 0xDD, 0x1B);
        Add("Backslash", 0xDC, 0x2B);
        Add("Semicolon", 0xBA, 0x27);
        Add("Quote", 0xDE, 0x28);
        Add("Comma", 0xBC, 0x33);
        Add("Period", 0xBE, 0x34);
        Add("Slash", 0xBF, 0x35);
        Add("Enter", 0x0D, 0x1C);
        Add("Space", 0x20, 0x39);
        Add("CapsLock", 0x14, 0x3A);

        Add("PrintScreen", 0x2C, 0x37, extended: true);
        Add("ScrollLock", 0x91, 0x46);
        Add("Pause", 0x13, 0x45);
        Add("Insert", 0x2D, 0x52, extended: true);
        Add("Delete", 0x2E, 0x53, extended: true);
        Add("Home", 0x24, 0x47, extended: true);
        Add("End", 0x23, 0x4F, extended: true);
        Add("PageUp", 0x21, 0x49, extended: true);
        Add("PageDown", 0x22, 0x51, extended: true);
        Add("Up", 0x26, 0x48, extended: true);
        Add("Down", 0x28, 0x50, extended: true);
        Add("Left", 0x25, 0x4B, extended: true);
        Add("Right", 0x27, 0x4D, extended: true);
        Add("NumLock", 0x90, 0x45, extended: true);
        Add("Apps", 0x5D, 0x5D, extended: true); // the context-menu key

        Add("LControl", 0xA2, 0x1D);
        Add("RControl", 0xA3, 0x1D, extended: true);
        Add("LShift", 0xA0, 0x2A);
        Add("RShift", 0xA1, 0x36);
        Add("LAlt", 0xA4, 0x38);
        Add("RAlt", 0xA5, 0x38, extended: true);
        Add("LWin", 0x5B, 0x5B, extended: true);
        Add("RWin", 0x5C, 0x5C, extended: true);
        return keys;
    }
}
