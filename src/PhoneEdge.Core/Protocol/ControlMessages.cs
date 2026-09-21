using System.Buffers.Binary;
using System.Text;

namespace PhoneEdge.Core.Protocol;

/// <summary>Encoders for the scrcpy 4.1 client→server control messages we use.</summary>
public static class ControlMessages
{
    public const byte TypeInjectKeycode = 0;
    public const byte TypeBackOrScreenOn = 4;
    public const byte TypeUhidCreate = 12;
    public const byte TypeUhidInput = 13;
    public const byte TypeUhidDestroy = 14;

    /// <summary>Android KeyEvent.KEYCODE_POWER; a press/release locks the phone like the physical button.</summary>
    public const int KeycodePower = 26;

    /// <summary>Injects an Android key event: action (0 down / 1 up), keycode, repeat 0, no meta state.</summary>
    public static byte[] InjectKeycode(bool down, int keycode)
    {
        var buf = new byte[14];
        buf[0] = TypeInjectKeycode;
        buf[1] = down ? (byte)0 : (byte)1;
        BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(2), keycode);
        // bytes 6..13: repeat and metaState stay 0
        return buf;
    }

    /// <summary>Injects KEYCODE_BACK (or wakes the screen if it is off). Action byte: 0 = down, 1 = up.</summary>
    public static byte[] BackOrScreenOn(bool down) => [TypeBackOrScreenOn, down ? (byte)0 : (byte)1];

    public static byte[] UhidCreate(ushort id, ushort vendorId, ushort productId, string name, ReadOnlySpan<byte> reportDesc)
    {
        var nameBytes = Utf8Truncate(name, 127);
        var buf = new byte[1 + 2 + 2 + 2 + 1 + nameBytes.Length + 2 + reportDesc.Length];
        buf[0] = TypeUhidCreate;
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(1), id);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(3), vendorId);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(5), productId);
        buf[7] = (byte)nameBytes.Length;
        nameBytes.CopyTo(buf, 8);
        int i = 8 + nameBytes.Length;
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(i), (ushort)reportDesc.Length);
        reportDesc.CopyTo(buf.AsSpan(i + 2));
        return buf;
    }

    public static byte[] UhidInput(ushort id, ReadOnlySpan<byte> data)
    {
        var buf = new byte[5 + data.Length];
        buf[0] = TypeUhidInput;
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(1), id);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(3), (ushort)data.Length);
        data.CopyTo(buf.AsSpan(5));
        return buf;
    }

    public static byte[] UhidDestroy(ushort id)
    {
        var buf = new byte[3];
        buf[0] = TypeUhidDestroy;
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(1), id);
        return buf;
    }

    /// <summary>UTF-8 encode and truncate to at most maxBytes without splitting a code point.</summary>
    private static byte[] Utf8Truncate(string s, int maxBytes)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        if (bytes.Length <= maxBytes) return bytes;
        int len = maxBytes;
        while (len > 0 && (bytes[len] & 0xC0) == 0x80) len--; // back off to a code point boundary
        return bytes[..len];
    }
}
