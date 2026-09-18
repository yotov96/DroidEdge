using System.Buffers.Binary;
using System.Text;

namespace PhoneEdge.Core.Protocol;

public abstract record DeviceMessage;
public sealed record ClipboardMessage(string Text) : DeviceMessage;
public sealed record AckClipboardMessage(ulong Sequence) : DeviceMessage;
public sealed record UhidOutputMessage(ushort Id, byte[] Data) : DeviceMessage;

/// <summary>Blocking parser for scrcpy 4.1 server→client device messages.</summary>
public static class DeviceMessageReader
{
    public static DeviceMessage? Read(Stream s)
    {
        int type = s.ReadByte();
        if (type < 0) return null;
        switch (type)
        {
            case 0:
            {
                var len = BinaryPrimitives.ReadUInt32BigEndian(ReadExactly(s, 4));
                if (len > 1 << 20) throw new IOException($"Clipboard message too large: {len}");
                return new ClipboardMessage(Encoding.UTF8.GetString(ReadExactly(s, (int)len)));
            }
            case 1:
                return new AckClipboardMessage(BinaryPrimitives.ReadUInt64BigEndian(ReadExactly(s, 8)));
            case 2:
            {
                var id = BinaryPrimitives.ReadUInt16BigEndian(ReadExactly(s, 2));
                var len = BinaryPrimitives.ReadUInt16BigEndian(ReadExactly(s, 2));
                return new UhidOutputMessage(id, ReadExactly(s, len));
            }
            default:
                throw new IOException($"Unknown device message type {type}");
        }
    }

    private static byte[] ReadExactly(Stream s, int count)
    {
        var buf = new byte[count];
        int off = 0;
        while (off < count)
        {
            int n = s.Read(buf, off, count - off);
            if (n <= 0) throw new IOException("Unexpected end of stream in device message");
            off += n;
        }
        return buf;
    }
}
