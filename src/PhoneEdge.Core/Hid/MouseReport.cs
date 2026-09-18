namespace PhoneEdge.Core.Hid;

[Flags]
public enum HidMouseButtons : byte
{
    None = 0,
    Left = 1,
    Right = 2,
    Middle = 4,
    Button4 = 8,
    Button5 = 16,
}

public static class MouseReport
{
    public const int Size = 5;

    public static byte[] Encode(HidMouseButtons buttons, sbyte dx, sbyte dy, sbyte wheel, sbyte pan) =>
        [(byte)buttons, (byte)dx, (byte)dy, (byte)wheel, (byte)pan];
}
