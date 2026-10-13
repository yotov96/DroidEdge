namespace DroidEdge.Core.Hid;

/// <summary>PS/2 scan code set 1 (as delivered by Windows Raw Input MakeCode) → USB HID keyboard usage.</summary>
public static class ScancodeMap
{
    // Index = make code without E0 prefix. 0 = unmapped.
    private static readonly byte[] Plain = BuildPlain();
    private static readonly Dictionary<ushort, byte> Extended = new()
    {
        [0x1C] = 0x58, // Keypad Enter
        [0x1D] = 0xE4, // Right Ctrl
        [0x35] = 0x54, // Keypad /
        [0x37] = 0x46, // Print Screen
        [0x38] = 0xE6, // Right Alt
        [0x45] = 0x53, // Num Lock (Raw Input reports it with E0)
        [0x47] = 0x4A, // Home
        [0x48] = 0x52, // Up
        [0x49] = 0x4B, // Page Up
        [0x4B] = 0x50, // Left
        [0x4D] = 0x4F, // Right
        [0x4F] = 0x4D, // End
        [0x50] = 0x51, // Down
        [0x51] = 0x4E, // Page Down
        [0x52] = 0x49, // Insert
        [0x53] = 0x4C, // Delete
        [0x5B] = 0xE3, // Left GUI
        [0x5C] = 0xE7, // Right GUI
        [0x5D] = 0x65, // Application / Menu
    };

    public static bool TryMap(ushort makeCode, bool e0, out byte usage)
    {
        if (e0)
        {
            return Extended.TryGetValue(makeCode, out usage);
        }
        if (makeCode < Plain.Length && Plain[makeCode] != 0)
        {
            usage = Plain[makeCode];
            return true;
        }
        usage = 0;
        return false;
    }

    private static byte[] BuildPlain()
    {
        var t = new byte[0x80];
        t[0x01] = 0x29; // Esc
        byte[] digits = [0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27]; // 1..9,0
        for (int i = 0; i < 10; i++) t[0x02 + i] = digits[i];
        t[0x0C] = 0x2D; // -
        t[0x0D] = 0x2E; // =
        t[0x0E] = 0x2A; // Backspace
        t[0x0F] = 0x2B; // Tab
        byte[] row1 = [0x14, 0x1A, 0x08, 0x15, 0x17, 0x1C, 0x18, 0x0C, 0x12, 0x13, 0x2F, 0x30]; // Q W E R T Y U I O P [ ]
        for (int i = 0; i < row1.Length; i++) t[0x10 + i] = row1[i];
        t[0x1C] = 0x28; // Enter
        t[0x1D] = 0xE0; // Left Ctrl
        byte[] row2 = [0x04, 0x16, 0x07, 0x09, 0x0A, 0x0B, 0x0D, 0x0E, 0x0F, 0x33, 0x34, 0x35]; // A S D F G H J K L ; ' `
        for (int i = 0; i < row2.Length; i++) t[0x1E + i] = row2[i];
        t[0x2A] = 0xE1; // Left Shift
        t[0x2B] = 0x31; // Backslash
        byte[] row3 = [0x1D, 0x1B, 0x06, 0x19, 0x05, 0x11, 0x10, 0x36, 0x37, 0x38]; // Z X C V B N M , . /
        for (int i = 0; i < row3.Length; i++) t[0x2C + i] = row3[i];
        t[0x36] = 0xE5; // Right Shift
        t[0x37] = 0x55; // Keypad *
        t[0x38] = 0xE2; // Left Alt
        t[0x39] = 0x2C; // Space
        t[0x3A] = 0x39; // Caps Lock
        for (int i = 0; i < 10; i++) t[0x3B + i] = (byte)(0x3A + i); // F1..F10
        t[0x45] = 0x53; // Num Lock (without E0 on some keyboards)
        t[0x46] = 0x47; // Scroll Lock
        t[0x47] = 0x5F; t[0x48] = 0x60; t[0x49] = 0x61; // KP 7 8 9
        t[0x4A] = 0x56; // KP -
        t[0x4B] = 0x5C; t[0x4C] = 0x5D; t[0x4D] = 0x5E; // KP 4 5 6
        t[0x4E] = 0x57; // KP +
        t[0x4F] = 0x59; t[0x50] = 0x5A; t[0x51] = 0x5B; // KP 1 2 3
        t[0x52] = 0x62; // KP 0
        t[0x53] = 0x63; // KP .
        t[0x56] = 0x64; // ISO extra key (\| on UK/DE keyboards)
        t[0x57] = 0x44; // F11
        t[0x58] = 0x45; // F12
        return t;
    }
}
