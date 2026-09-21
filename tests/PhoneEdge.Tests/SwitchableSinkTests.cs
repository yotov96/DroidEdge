using PhoneEdge.App;
using PhoneEdge.Core.Input;

namespace PhoneEdge.Tests;

public class SwitchableSinkTests
{
    private sealed class Rec : IReportSink
    {
        public int Mouse, Keyboard, Back, Power;
        public bool SendMouse(byte[] r) { Mouse++; return true; }
        public bool SendKeyboard(byte[] r) { Keyboard++; return true; }
        public bool SendBack(bool down) { Back++; return true; }
        public bool SendPower(bool down) { Power++; return true; }
    }

    [Fact]
    public void Drops_without_target_and_forwards_with_target()
    {
        var s = new SwitchableSink();
        Assert.False(s.SendMouse(new byte[5]));
        Assert.False(s.SendBack(true));
        Assert.False(s.SendPower(true));
        var t = new Rec();
        s.Target = t;
        Assert.True(s.SendMouse(new byte[5]));
        Assert.True(s.SendKeyboard(new byte[8]));
        Assert.True(s.SendBack(true));
        Assert.True(s.SendPower(true));
        Assert.Equal((1, 1, 1, 1), (t.Mouse, t.Keyboard, t.Back, t.Power));
        s.Target = null;
        Assert.False(s.SendKeyboard(new byte[8]));
    }
}
