namespace PhoneEdge.Core;

public enum Edge { Left, Right, Top, Bottom }

/// <summary>Integer rectangle; Right and Bottom are exclusive.</summary>
public readonly record struct IntRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;
}
