using PhoneEdge.Core.Protocol;

namespace PhoneEdge.Tests;

public class ControlMessagesTests
{
    [Fact]
    public void UhidCreate_matches_scrcpy_layout()
    {
        byte[] desc = [0x05, 0x01, 0x09, 0x02];
        var msg = ControlMessages.UhidCreate(1, 0x1234, 0xABCD, "Ab", desc);
        byte[] expected =
        [
            12,             // type
            0x00, 0x01,     // id
            0x12, 0x34,     // vendor
            0xAB, 0xCD,     // product
            2, (byte)'A', (byte)'b', // name length + name
            0x00, 0x04,     // desc length
            0x05, 0x01, 0x09, 0x02,
        ];
        Assert.Equal(expected, msg);
    }

    [Fact]
    public void UhidCreate_with_empty_name_writes_zero_length()
    {
        var msg = ControlMessages.UhidCreate(2, 0, 0, "", [0xC0]);
        Assert.Equal(new byte[] { 12, 0, 2, 0, 0, 0, 0, 0, 0, 1, 0xC0 }, msg);
    }

    [Fact]
    public void UhidCreate_truncates_name_to_127_utf8_bytes()
    {
        var msg = ControlMessages.UhidCreate(1, 0, 0, new string('x', 300), []);
        Assert.Equal(127, msg[7]);
        Assert.Equal(1 + 6 + 1 + 127 + 2, msg.Length);
    }

    [Fact]
    public void UhidInput_matches_scrcpy_layout()
    {
        var msg = ControlMessages.UhidInput(1, [0x01, 0xFF, 0x00, 0x00, 0x00]);
        Assert.Equal(new byte[] { 13, 0, 1, 0, 5, 0x01, 0xFF, 0, 0, 0 }, msg);
    }

    [Fact]
    public void UhidDestroy_matches_scrcpy_layout()
    {
        Assert.Equal(new byte[] { 14, 0x01, 0x02 }, ControlMessages.UhidDestroy(0x0102));
    }

    [Fact]
    public void SetDisplayPower_matches_scrcpy_layout()
    {
        // type 10, then a boolean byte
        Assert.Equal(new byte[] { 10, 1 }, ControlMessages.SetDisplayPower(on: true));
        Assert.Equal(new byte[] { 10, 0 }, ControlMessages.SetDisplayPower(on: false));
    }

    [Fact]
    public void SetClipboard_matches_scrcpy_layout()
    {
        // type 9, u64 sequence, u8 paste, u32 length, UTF-8 text
        var msg = ControlMessages.SetClipboard(0x0102, "hé", paste: false);
        Assert.Equal(new byte[] { 9, 0, 0, 0, 0, 0, 0, 0x01, 0x02, 0, 0, 0, 0, 3, (byte)'h', 0xC3, 0xA9 }, msg);
    }

    [Fact]
    public void SetClipboard_rejects_text_over_the_server_limit()
    {
        Assert.Throws<ArgumentException>(() => ControlMessages.SetClipboard(0, new string('x', ControlMessages.MaxClipboardTextBytes + 1), false));
        Assert.Equal(14 + ControlMessages.MaxClipboardTextBytes, ControlMessages.SetClipboard(0, new string('x', ControlMessages.MaxClipboardTextBytes), false).Length);
    }

    [Fact]
    public void ScanFile_matches_scrcpy_layout()
    {
        // type 22, u32 length, UTF-8 path
        Assert.Equal(new byte[] { 22, 0, 0, 0, 3, (byte)'/', (byte)'a', (byte)'b' }, ControlMessages.ScanFile("/ab"));
        Assert.Throws<ArgumentException>(() => ControlMessages.ScanFile(new string('x', 257)));
    }

    [Fact]
    public void BackOrScreenOn_matches_scrcpy_layout()
    {
        // type 4, then the Android key action: 0 = down, 1 = up
        Assert.Equal(new byte[] { 4, 0 }, ControlMessages.BackOrScreenOn(down: true));
        Assert.Equal(new byte[] { 4, 1 }, ControlMessages.BackOrScreenOn(down: false));
    }
}
