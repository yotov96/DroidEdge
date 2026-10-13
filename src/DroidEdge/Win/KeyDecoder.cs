using DroidEdge.Core.Hid;

namespace DroidEdge.Win;

/// <summary>
/// Turns a Windows keyboard event (PS/2 set 1 scan code + extended flag + virtual key) into a HID usage.
/// Used by the low-level keyboard hook: keystrokes must come from the hook because a key the hook swallows
/// never reaches Raw Input.
/// </summary>
public static class KeyDecoder
{
    private const uint VkPause = 0x13;

    public static bool TryDecode(ushort scanCode, bool extended, uint vkCode, out byte usage)
    {
        usage = 0;
        if (scanCode == 0 || scanCode == 0xFF || vkCode == 0xFF) return false; // fake/overrun keys
        if (vkCode == VkPause)
        {
            usage = 0x48;
            return true;
        }
        return ScancodeMap.TryMap(scanCode, extended, out usage);
    }
}
