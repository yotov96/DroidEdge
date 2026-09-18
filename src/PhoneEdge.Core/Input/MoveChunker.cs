namespace PhoneEdge.Core.Input;

public static class MoveChunker
{
    private const int Max = 127;

    /// <summary>Split a delta into pieces that fit in signed bytes.</summary>
    public static IEnumerable<(sbyte dx, sbyte dy)> Chunk(int dx, int dy)
    {
        while (dx != 0 || dy != 0)
        {
            int sx = Math.Clamp(dx, -Max, Max);
            int sy = Math.Clamp(dy, -Max, Max);
            dx -= sx;
            dy -= sy;
            yield return ((sbyte)sx, (sbyte)sy);
        }
    }

    /// <summary>Moves that first pin the real cursor at the top-left corner (overshooting the display size) and then travel to the target.</summary>
    public static IEnumerable<(sbyte dx, sbyte dy)> Resync(int width, int height, int targetX, int targetY)
    {
        foreach (var m in Chunk(-(width + Max), -(height + Max))) yield return m;
        foreach (var m in Chunk(targetX, targetY)) yield return m;
    }
}
