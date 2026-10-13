using DroidEdge.Core.Config;
using DroidEdge.Win;

namespace DroidEdge.Tests;

public class ScrcpyShortcutKeyTests
{
    [Fact]
    public void Every_key_the_config_accepts_has_a_windows_key()
    {
        foreach (string name in ConfigLoader.ShortcutModKeys)
            Assert.NotNull(ScrcpyShortcutKey.FromName(name));
    }

    [Fact]
    public void Right_hand_keys_use_the_extended_scan_code()
    {
        Assert.Equal(new ScrcpyShortcutKey(0xA3, 0x1D, true), ScrcpyShortcutKey.FromName("rctrl"));
        Assert.Equal(new ScrcpyShortcutKey(0xA2, 0x1D, false), ScrcpyShortcutKey.FromName("lctrl"));
        Assert.Equal(new ScrcpyShortcutKey(0xA5, 0x38, true), ScrcpyShortcutKey.FromName("ralt"));
        Assert.Null(ScrcpyShortcutKey.FromName("tilde"));
    }
}
