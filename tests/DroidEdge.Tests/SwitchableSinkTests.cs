using DroidEdge.App;
using DroidEdge.Core.Input;

namespace DroidEdge.Tests;

public class SwitchableSinkTests
{
    private sealed class Rec : IReportSink
    {
        public int Mouse, Keyboard, Back, DisplayPower;
        public bool SendMouse(byte[] r) { Mouse++; return true; }
        public bool SendKeyboard(byte[] r) { Keyboard++; return true; }
        public bool SendBack(bool down) { Back++; return true; }
        public bool SendDisplayPower(bool on) { DisplayPower++; return true; }
    }

    [Fact]
    public void Drops_without_target_and_forwards_with_target()
    {
        var s = new SwitchableSink();
        Assert.False(s.SendMouse(new byte[5]));
        Assert.False(s.SendBack(true));
        Assert.False(s.SendDisplayPower(false));
        var t = new Rec();
        s.Target = t;
        Assert.True(s.SendMouse(new byte[5]));
        Assert.True(s.SendKeyboard(new byte[8]));
        Assert.True(s.SendBack(true));
        Assert.True(s.SendDisplayPower(false));
        Assert.Equal((1, 1, 1, 1), (t.Mouse, t.Keyboard, t.Back, t.DisplayPower));
        s.Target = null;
        Assert.False(s.SendKeyboard(new byte[8]));
    }
}
