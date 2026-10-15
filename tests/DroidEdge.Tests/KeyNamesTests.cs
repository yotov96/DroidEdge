using DroidEdge.Core.Input;
using DroidEdge.Win;

namespace DroidEdge.Tests;

public class KeyNamesTests
{
    [Fact]
    public void Names_and_physical_keys_are_unique()
    {
        Assert.Equal(KeyNames.All.Count, KeyNames.All.Select(k => k.Name.ToLowerInvariant()).Distinct().Count());
        Assert.Equal(KeyNames.All.Count, KeyNames.All.Select(k => (k.ScanCode, k.Extended)).Distinct().Count());
        Assert.Equal(KeyNames.All.Count, KeyNames.All.Select(k => k.VirtualKey).Distinct().Count());
    }

    [Fact]
    public void Scan_codes_agree_with_the_keyboard_decoder()
    {
        // The decoder turns the hook's scan code and virtual key into the HID usage sent to the phone; F13-F24 have no
        // usage there, so they can be named but are not forwarded
        foreach (KeyInfo key in KeyNames.All.Where(k => !(k.Name.StartsWith('F') && int.TryParse(k.Name[1..], out int n) && n >= 13)))
            Assert.True(KeyDecoder.TryDecode(key.ScanCode, key.Extended, (uint)key.VirtualKey, out _), key.Name);
    }

    [Fact]
    public void Find_ignores_case_and_spaces()
    {
        Assert.Equal(0xC0, KeyNames.Find(" tilde ")!.VirtualKey);
        Assert.Equal((ushort)0x29, KeyNames.Find("Tilde")!.ScanCode);
        Assert.Equal('D', KeyNames.Find("d")!.VirtualKey);
        Assert.Null(KeyNames.Find("Hyper"));
    }

    [Fact]
    public void Hotkeys_parse_into_modifiers_and_a_key()
    {
        Assert.True(Hotkey.TryParse("ctrl + Alt + d", out Hotkey? hotkey, out _));
        Assert.Equal(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, hotkey!.Modifiers);
        Assert.Equal("D", hotkey.Key.Name);
        Assert.Equal("Ctrl+Alt+D", hotkey.ToString());

        Assert.True(Hotkey.TryParse("F13", out Hotkey? single, out _));
        Assert.Equal(HotkeyModifiers.None, single!.Modifiers);
    }

    [Theory]
    [InlineData("Ctrl+Alt")]       // no key
    [InlineData("Ctrl+Hyper")]     // unknown key
    [InlineData("Meh+F")]          // unknown modifier
    [InlineData("Ctrl+LWin")]      // a modifier key as the key
    [InlineData("Ctrl++D")]
    [InlineData("")]
    public void Invalid_hotkeys_are_rejected(string text)
    {
        Assert.False(Hotkey.TryParse(text, out Hotkey? hotkey, out string error));
        Assert.Null(hotkey);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void Release_key_binding_needs_a_known_key_that_is_not_the_shortcut_key()
    {
        ReleaseKeyBinding? binding = ReleaseKeyBinding.Create("Tilde", "rctrl");
        Assert.Equal((ushort)0x29, binding!.Key.ScanCode);
        Assert.Equal(ScrcpyShortcutKey.FromName("rctrl"), binding.ShortcutKey);
        Assert.Null(ReleaseKeyBinding.Create("RControl", "rctrl"));
        Assert.Null(ReleaseKeyBinding.Create("Hyper", "rctrl"));
    }
}
