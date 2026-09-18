using PhoneEdge.App;
using PhoneEdge.Core.Input;

namespace PhoneEdge.Tests;

public class SwitchableSinkTests
{
    private sealed class Rec : IReportSink
    {
        public int Mouse, Keyboard;
        public bool SendMouse(byte[] r) { Mouse++; return true; }
        public bool SendKeyboard(byte[] r) { Keyboard++; return true; }
    }

    [Fact]
    public void Drops_without_target_and_forwards_with_target()
    {
        var s = new SwitchableSink();
        Assert.False(s.SendMouse(new byte[5]));
        var t = new Rec();
        s.Target = t;
        Assert.True(s.SendMouse(new byte[5]));
        Assert.True(s.SendKeyboard(new byte[8]));
        Assert.Equal((1, 1), (t.Mouse, t.Keyboard));
        s.Target = null;
        Assert.False(s.SendKeyboard(new byte[8]));
    }
}
