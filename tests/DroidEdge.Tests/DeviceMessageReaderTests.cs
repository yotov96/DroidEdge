using DroidEdge.Core.Protocol;

namespace DroidEdge.Tests;

public class DeviceMessageReaderTests
{
    [Fact]
    public void Reads_clipboard_message()
    {
        var s = new MemoryStream([0, 0, 0, 0, 2, (byte)'h', (byte)'i']);
        var m = Assert.IsType<ClipboardMessage>(DeviceMessageReader.Read(s));
        Assert.Equal("hi", m.Text);
    }

    [Fact]
    public void Reads_ack_clipboard_message()
    {
        var s = new MemoryStream([1, 0, 0, 0, 0, 0, 0, 0x01, 0x02]);
        var m = Assert.IsType<AckClipboardMessage>(DeviceMessageReader.Read(s));
        Assert.Equal(0x0102UL, m.Sequence);
    }

    [Fact]
    public void Reads_uhid_output_message()
    {
        var s = new MemoryStream([2, 0, 2, 0, 1, 0x07]);
        var m = Assert.IsType<UhidOutputMessage>(DeviceMessageReader.Read(s));
        Assert.Equal(2, m.Id);
        Assert.Equal(new byte[] { 0x07 }, m.Data);
    }

    [Fact]
    public void Returns_null_on_clean_eof()
    {
        Assert.Null(DeviceMessageReader.Read(new MemoryStream()));
    }

    [Fact]
    public void Throws_on_truncated_message()
    {
        Assert.Throws<IOException>(() => DeviceMessageReader.Read(new MemoryStream([2, 0, 2, 0, 5, 0x07])));
    }

    [Fact]
    public void Throws_on_unknown_type()
    {
        Assert.Throws<IOException>(() => DeviceMessageReader.Read(new MemoryStream([9])));
    }
}
