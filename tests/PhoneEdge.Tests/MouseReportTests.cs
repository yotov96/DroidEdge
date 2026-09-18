using PhoneEdge.Core.Hid;

namespace PhoneEdge.Tests;

public class MouseReportTests
{
    [Fact]
    public void Encodes_buttons_and_axes()
    {
        var r = MouseReport.Encode(HidMouseButtons.Left | HidMouseButtons.Middle, -3, 5, 1, -1);
        Assert.Equal(new byte[] { 0b101, 0xFD, 0x05, 0x01, 0xFF }, r);
    }

    [Fact]
    public void Descriptor_bytes_match_scrcpy()
    {
        Assert.Equal(0x05, HidDescriptors.Mouse[0]);
        Assert.Equal(67, HidDescriptors.Mouse.Length);
        Assert.Equal(0xC0, HidDescriptors.Mouse[^1]);
        Assert.Equal(63, HidDescriptors.Keyboard.Length);
        Assert.Equal(0xC0, HidDescriptors.Keyboard[^1]);
    }
}
