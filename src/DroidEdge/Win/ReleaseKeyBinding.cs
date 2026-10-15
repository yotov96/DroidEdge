using DroidEdge.Core.Input;

namespace DroidEdge.Win;

/// <summary>The desktop window's release key (desktopReleaseKey) and the scrcpy shortcut key it taps.</summary>
public sealed record ReleaseKeyBinding(KeyInfo Key, ScrcpyShortcutKey ShortcutKey)
{
    /// <returns>
    /// The binding, or null when either name is unknown or the release key already is the shortcut key, which needs no
    /// remapping.
    /// </returns>
    public static ReleaseKeyBinding? Create(string releaseKeyName, string shortcutKeyName)
    {
        KeyInfo? key = KeyNames.Find(releaseKeyName);
        ScrcpyShortcutKey? shortcut = ScrcpyShortcutKey.FromName(shortcutKeyName);
        if (key is null || shortcut is null || key.VirtualKey == shortcut.VirtualKey) return null;
        return new ReleaseKeyBinding(key, shortcut);
    }
}
