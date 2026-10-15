using DroidEdge.Core.Input;

namespace DroidEdge.Tests;

public class ReleaseKeyRemapTests
{
    [Fact]
    public void Plain_press_in_the_desktop_window_taps_the_shortcut_key_and_swallows_the_key()
    {
        ReleaseKeyRemap r = new();
        Assert.Equal(ReleaseKeyAction.SwallowAndTapShortcutKey, r.OnKey(down: true, modifierHeld: false, desktopActive: true));
        Assert.Equal(ReleaseKeyAction.Swallow, r.OnKey(down: false, modifierHeld: false, desktopActive: true));
    }

    [Fact]
    public void Auto_repeat_while_held_taps_only_once()
    {
        ReleaseKeyRemap r = new();
        Assert.Equal(ReleaseKeyAction.SwallowAndTapShortcutKey, r.OnKey(true, false, true));
        Assert.Equal(ReleaseKeyAction.Swallow, r.OnKey(true, false, true));
        Assert.Equal(ReleaseKeyAction.Swallow, r.OnKey(true, false, true));
        Assert.Equal(ReleaseKeyAction.Swallow, r.OnKey(false, false, true));
        Assert.Equal(ReleaseKeyAction.SwallowAndTapShortcutKey, r.OnKey(true, false, true));
    }

    [Fact]
    public void Other_windows_and_combinations_pass_through()
    {
        ReleaseKeyRemap r = new();
        Assert.Equal(ReleaseKeyAction.PassThrough, r.OnKey(true, false, desktopActive: false));
        Assert.Equal(ReleaseKeyAction.PassThrough, r.OnKey(false, false, false));
        Assert.Equal(ReleaseKeyAction.PassThrough, r.OnKey(true, modifierHeld: true, desktopActive: true));
        Assert.Equal(ReleaseKeyAction.PassThrough, r.OnKey(false, true, true));
    }

    [Fact]
    public void Release_follows_the_press_when_the_active_window_changes_in_between()
    {
        ReleaseKeyRemap r = new();
        Assert.Equal(ReleaseKeyAction.SwallowAndTapShortcutKey, r.OnKey(true, false, true));
        Assert.Equal(ReleaseKeyAction.Swallow, r.OnKey(false, false, desktopActive: false));

        Assert.Equal(ReleaseKeyAction.PassThrough, r.OnKey(true, false, desktopActive: false));
        Assert.Equal(ReleaseKeyAction.PassThrough, r.OnKey(true, false, desktopActive: true)); // repeat
        Assert.Equal(ReleaseKeyAction.PassThrough, r.OnKey(false, false, desktopActive: true));
    }

    [Fact]
    public void Press_held_with_a_modifier_does_not_tap_when_the_modifier_is_released_first()
    {
        ReleaseKeyRemap r = new();
        Assert.Equal(ReleaseKeyAction.PassThrough, r.OnKey(true, modifierHeld: true, desktopActive: true));
        Assert.Equal(ReleaseKeyAction.PassThrough, r.OnKey(true, modifierHeld: false, desktopActive: true)); // repeat
        Assert.Equal(ReleaseKeyAction.PassThrough, r.OnKey(false, false, true));
    }
}
