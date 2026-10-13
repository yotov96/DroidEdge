using DroidEdge.Core.Input;

namespace DroidEdge.Tests;

public class TildeKeyRemapTests
{
    private const ushort Tilde = TildeKeyRemap.ScanCode;

    [Fact]
    public void Plain_press_in_the_desktop_window_taps_right_control_and_swallows_the_key()
    {
        TildeKeyRemap r = new();
        Assert.Equal(TildeKeyAction.SwallowAndTapRightControl, r.OnKey(Tilde, false, down: true, modifierHeld: false, desktopActive: true));
        Assert.Equal(TildeKeyAction.Swallow, r.OnKey(Tilde, false, down: false, modifierHeld: false, desktopActive: true));
    }

    [Fact]
    public void Auto_repeat_while_held_taps_only_once()
    {
        TildeKeyRemap r = new();
        Assert.Equal(TildeKeyAction.SwallowAndTapRightControl, r.OnKey(Tilde, false, true, false, true));
        Assert.Equal(TildeKeyAction.Swallow, r.OnKey(Tilde, false, true, false, true));
        Assert.Equal(TildeKeyAction.Swallow, r.OnKey(Tilde, false, true, false, true));
        Assert.Equal(TildeKeyAction.Swallow, r.OnKey(Tilde, false, false, false, true));
        Assert.Equal(TildeKeyAction.SwallowAndTapRightControl, r.OnKey(Tilde, false, true, false, true));
    }

    [Fact]
    public void Other_windows_and_combinations_pass_through()
    {
        TildeKeyRemap r = new();
        Assert.Equal(TildeKeyAction.PassThrough, r.OnKey(Tilde, false, true, false, desktopActive: false));
        Assert.Equal(TildeKeyAction.PassThrough, r.OnKey(Tilde, false, false, false, false));
        Assert.Equal(TildeKeyAction.PassThrough, r.OnKey(Tilde, false, true, modifierHeld: true, desktopActive: true));
        Assert.Equal(TildeKeyAction.PassThrough, r.OnKey(Tilde, false, false, true, true));
    }

    [Fact]
    public void Other_keys_pass_through()
    {
        TildeKeyRemap r = new();
        Assert.Equal(TildeKeyAction.PassThrough, r.OnKey(0x1E, false, true, false, true)); // A
        Assert.Equal(TildeKeyAction.PassThrough, r.OnKey(Tilde, extended: true, down: true, modifierHeld: false, desktopActive: true));
    }

    [Fact]
    public void Release_follows_the_press_when_the_active_window_changes_in_between()
    {
        TildeKeyRemap r = new();
        Assert.Equal(TildeKeyAction.SwallowAndTapRightControl, r.OnKey(Tilde, false, true, false, true));
        Assert.Equal(TildeKeyAction.Swallow, r.OnKey(Tilde, false, false, false, desktopActive: false));

        Assert.Equal(TildeKeyAction.PassThrough, r.OnKey(Tilde, false, true, false, desktopActive: false));
        Assert.Equal(TildeKeyAction.PassThrough, r.OnKey(Tilde, false, true, false, desktopActive: true)); // repeat
        Assert.Equal(TildeKeyAction.PassThrough, r.OnKey(Tilde, false, false, false, desktopActive: true));
    }

    [Fact]
    public void Press_held_with_a_modifier_does_not_tap_when_the_modifier_is_released_first()
    {
        TildeKeyRemap r = new();
        Assert.Equal(TildeKeyAction.PassThrough, r.OnKey(Tilde, false, true, modifierHeld: true, desktopActive: true));
        Assert.Equal(TildeKeyAction.PassThrough, r.OnKey(Tilde, false, true, modifierHeld: false, desktopActive: true)); // repeat
        Assert.Equal(TildeKeyAction.PassThrough, r.OnKey(Tilde, false, false, false, true));
    }
}
