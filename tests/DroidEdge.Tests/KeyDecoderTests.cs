using DroidEdge.Win;

namespace DroidEdge.Tests;

public class KeyDecoderTests
{
    [Theory]
    [InlineData(0x1E, false, 0x41, 0x04)] // A
    [InlineData(0x1D, true, 0xA3, 0xE4)]  // Right Ctrl (extended)
    [InlineData(0x48, true, 0x26, 0x52)]  // Up arrow
    [InlineData(0x45, true, 0x90, 0x53)]  // Num Lock arrives extended in the LL hook
    public void Decodes_scan_codes_like_the_raw_input_path(int scanCode, bool extended, int vk, int expected)
    {
        Assert.True(KeyDecoder.TryDecode((ushort)scanCode, extended, (uint)vk, out var usage));
        Assert.Equal((byte)expected, usage);
    }

    [Fact]
    public void Pause_is_decoded_from_the_virtual_key()
    {
        // The LL hook delivers Pause as scan code 0x45 with VK_PAUSE and no extended flag.
        Assert.True(KeyDecoder.TryDecode(0x45, false, 0x13, out var usage));
        Assert.Equal(0x48, usage);
    }

    [Theory]
    [InlineData(0x00, false, 0x41)]
    [InlineData(0xFF, false, 0x41)]
    [InlineData(0x1E, false, 0xFF)]
    [InlineData(0x7E, true, 0x41)]
    public void Fake_or_unknown_keys_are_rejected(int scanCode, bool extended, int vk)
    {
        Assert.False(KeyDecoder.TryDecode((ushort)scanCode, extended, (uint)vk, out _));
    }
}
