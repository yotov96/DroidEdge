using System.Buffers.Binary;
using System.Text;

namespace DroidEdge.Core.Protocol;

/// <summary>Encoders for the scrcpy 4.1 client→server control messages we use.</summary>
public static class ControlMessages
{
    public const byte TypeBackOrScreenOn = 4;
    public const byte TypeSetClipboard = 9;
    public const byte TypeSetDisplayPower = 10;
    public const byte TypeUhidCreate = 12;
    public const byte TypeUhidInput = 13;
    public const byte TypeUhidDestroy = 14;
    public const byte TypeScanFile = 22;

    /// <summary>The server reads control messages into a 256 KiB buffer; SET_CLIPBOARD has 14 bytes of header.</summary>
    public const int MaxClipboardTextBytes = (1 << 18) - 14;
    public const int MaxScanFilePathBytes = 256;

    /// <summary>Sets the phone clipboard. Sequence 0 asks for no acknowledgement; paste=true would also inject a paste.</summary>
    public static byte[] SetClipboard(ulong sequence, string text, bool paste)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        if (bytes.Length > MaxClipboardTextBytes)
            throw new ArgumentException($"clipboard text is {bytes.Length} bytes; the limit is {MaxClipboardTextBytes}", nameof(text));
        var buf = new byte[14 + bytes.Length];
        buf[0] = TypeSetClipboard;
        BinaryPrimitives.WriteUInt64BigEndian(buf.AsSpan(1), sequence);
        buf[9] = paste ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(10), (uint)bytes.Length);
        bytes.CopyTo(buf, 14);
        return buf;
    }

    /// <summary>Asks the phone to index a file it received so it shows up in Files/Photos.</summary>
    public static byte[] ScanFile(string devicePath)
    {
        var bytes = Encoding.UTF8.GetBytes(devicePath);
        if (bytes.Length > MaxScanFilePathBytes)
            throw new ArgumentException($"path is {bytes.Length} bytes; the limit is {MaxScanFilePathBytes}", nameof(devicePath));
        var buf = new byte[5 + bytes.Length];
        buf[0] = TypeScanFile;
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(1), (uint)bytes.Length);
        bytes.CopyTo(buf, 5);
        return buf;
    }

    /// <summary>
    /// Powers the phone's own display panel on or off. The phone stays awake and unlocked either way; the server
    /// turns the panel back on when it exits.
    /// </summary>
    public static byte[] SetDisplayPower(bool on) => [TypeSetDisplayPower, on ? (byte)1 : (byte)0];

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
