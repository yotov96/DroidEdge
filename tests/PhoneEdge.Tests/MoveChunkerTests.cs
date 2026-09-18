using PhoneEdge.Core.Input;

namespace PhoneEdge.Tests;

public class MoveChunkerTests
{
    [Fact]
    public void Small_move_is_single_chunk()
    {
        Assert.Equal([((sbyte)5, (sbyte)-7)], MoveChunker.Chunk(5, -7).ToArray());
    }

    [Fact]
    public void Large_move_is_split_and_sums_to_total()
    {
        var chunks = MoveChunker.Chunk(300, -260).ToArray();
        Assert.Equal(3, chunks.Length);
        Assert.Equal(300, chunks.Sum(c => (int)c.dx));
        Assert.Equal(-260, chunks.Sum(c => (int)c.dy));
        Assert.All(chunks, c => { Assert.InRange((int)c.dx, -127, 127); Assert.InRange((int)c.dy, -127, 127); });
    }

    [Fact]
    public void Zero_move_yields_nothing()
    {
        Assert.Empty(MoveChunker.Chunk(0, 0));
    }

    [Fact]
    public void Resync_pins_to_corner_then_moves_to_target()
    {
        var moves = MoveChunker.Resync(1344, 2992, 1343, 900).ToArray();
        // Pin phase: every chunk is non-positive on both axes; it must overshoot the display size.
        int i = 0, sx = 0, sy = 0;
        for (; i < moves.Length && moves[i].dx <= 0 && moves[i].dy <= 0; i++) { sx += moves[i].dx; sy += moves[i].dy; }
        Assert.True(sx <= -1344 && sy <= -2992, "pin phase must overshoot both axes");
        // Travel phase (all chunks non-negative) sums to the target.
        int tx = 0, ty = 0;
        for (; i < moves.Length; i++) { tx += moves[i].dx; ty += moves[i].dy; }
        Assert.Equal((1343, 900), (tx, ty));
    }
}
