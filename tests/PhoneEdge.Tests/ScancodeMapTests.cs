using PhoneEdge.Core.Hid;

namespace PhoneEdge.Tests;

public class ScancodeMapTests
{
    [Theory]
    [InlineData(0x1E, false, 0x04)] // A
    [InlineData(0x2C, false, 0x1D)] // Z
    [InlineData(0x02, false, 0x1E)] // 1
    [InlineData(0x0B, false, 0x27)] // 0
    [InlineData(0x1C, false, 0x28)] // Enter
    [InlineData(0x01, false, 0x29)] // Esc
    [InlineData(0x0E, false, 0x2A)] // Backspace
    [InlineData(0x39, false, 0x2C)] // Space
    [InlineData(0x1D, false, 0xE0)] // Left Ctrl
    [InlineData(0x1D, true, 0xE4)]  // Right Ctrl
    [InlineData(0x38, false, 0xE2)] // Left Alt
    [InlineData(0x38, true, 0xE6)]  // Right Alt
    [InlineData(0x5B, true, 0xE3)]  // Left Win
    [InlineData(0x48, true, 0x52)]  // Up
    [InlineData(0x50, true, 0x51)]  // Down
    [InlineData(0x4B, true, 0x50)]  // Left
    [InlineData(0x4D, true, 0x4F)]  // Right
    [InlineData(0x53, true, 0x4C)]  // Delete
    [InlineData(0x1C, true, 0x58)]  // Keypad Enter
    [InlineData(0x48, false, 0x60)] // Keypad 8
    [InlineData(0x3B, false, 0x3A)] // F1
    [InlineData(0x58, false, 0x45)] // F12
    [InlineData(0x45, true, 0x53)]  // NumLock (arrives with E0 in Raw Input)
    [InlineData(0x56, false, 0x64)] // ISO extra key
    public void Maps_known_scancodes(int make, bool e0, int expected)
    {
        Assert.True(ScancodeMap.TryMap((ushort)make, e0, out var usage));
        Assert.Equal((byte)expected, usage);
    }

    [Theory]
    [InlineData(0x00, false)]
    [InlineData(0xFF, false)]
    [InlineData(0x7E, true)]
    public void Unknown_scancodes_return_false(int make, bool e0)
    {
        Assert.False(ScancodeMap.TryMap((ushort)make, e0, out _));
    }
}
