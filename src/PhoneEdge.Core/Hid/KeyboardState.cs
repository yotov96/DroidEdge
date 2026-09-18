namespace PhoneEdge.Core.Hid;

/// <summary>Tracks pressed keys and produces boot-protocol keyboard reports (8 bytes).</summary>
public sealed class KeyboardState
{
    public const int ReportSize = 8;
    public const int MaxKeys = 6;
    public static readonly byte[] EmptyReport = new byte[ReportSize];

    private byte _modifiers;
    private readonly List<byte> _keys = new(MaxKeys + 1); // press order, oldest first

    public static bool IsModifier(byte usage) => usage is >= 0xE0 and <= 0xE7;

    /// <returns>true if state changed.</returns>
    public bool Press(byte usage)
    {
        if (usage == 0) return false;
        if (IsModifier(usage))
        {
            byte bit = (byte)(1 << (usage - 0xE0));
            if ((_modifiers & bit) != 0) return false;
            _modifiers |= bit;
            return true;
        }
        if (_keys.Contains(usage)) return false;
        _keys.Add(usage);
        if (_keys.Count > MaxKeys) _keys.RemoveAt(0);
        return true;
    }

    /// <returns>true if state changed.</returns>
    public bool Release(byte usage)
    {
        if (IsModifier(usage))
        {
            byte bit = (byte)(1 << (usage - 0xE0));
            if ((_modifiers & bit) == 0) return false;
            _modifiers &= (byte)~bit;
            return true;
        }
        return _keys.Remove(usage);
    }

    public void ReleaseAll()
    {
        _modifiers = 0;
        _keys.Clear();
    }

    public byte[] ToReport()
    {
        var r = new byte[ReportSize];
        r[0] = _modifiers;
        for (int i = 0; i < _keys.Count; i++) r[2 + i] = _keys[i];
        return r;
    }
}
