using PhoneEdge.Core.Hid;

namespace PhoneEdge.Tests;

public class KeyboardStateTests
{
    [Fact]
    public void Empty_state_is_all_zero()
    {
        Assert.Equal(new byte[8], new KeyboardState().ToReport());
    }

    [Fact]
    public void Modifiers_set_bits_and_do_not_use_key_slots()
    {
        var k = new KeyboardState();
        k.Press(0xE0); // left ctrl
        k.Press(0xE5); // right shift
        var r = k.ToReport();
        Assert.Equal(0b0010_0001, r[0]);
        Assert.All(r[2..], b => Assert.Equal(0, b));
    }

    [Fact]
    public void Keys_fill_slots_in_press_order_and_release_frees_slot()
    {
        var k = new KeyboardState();
        k.Press(0x04); k.Press(0x05);
        Assert.Equal(new byte[] { 0, 0, 0x04, 0x05, 0, 0, 0, 0 }, k.ToReport());
        k.Release(0x04);
        Assert.Equal(new byte[] { 0, 0, 0x05, 0, 0, 0, 0, 0 }, k.ToReport());
    }

    [Fact]
    public void Seventh_key_evicts_oldest()
    {
        var k = new KeyboardState();
        for (byte u = 0x04; u < 0x0B; u++) k.Press(u); // 7 keys
        var r = k.ToReport();
        Assert.DoesNotContain((byte)0x04, r[2..]);
        Assert.Contains((byte)0x0A, r[2..]);
    }

    [Fact]
    public void Press_returns_false_when_already_down_and_release_returns_false_when_not_down()
    {
        var k = new KeyboardState();
        Assert.True(k.Press(0x04));
        Assert.False(k.Press(0x04));
        Assert.True(k.Release(0x04));
        Assert.False(k.Release(0x04));
    }

    [Fact]
    public void ReleaseAll_clears_everything()
    {
        var k = new KeyboardState();
        k.Press(0xE1); k.Press(0x04);
        k.ReleaseAll();
        Assert.Equal(KeyboardState.EmptyReport, k.ToReport());
    }
}
