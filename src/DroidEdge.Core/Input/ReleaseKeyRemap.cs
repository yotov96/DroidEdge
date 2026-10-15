namespace DroidEdge.Core.Input;

public enum ReleaseKeyAction { PassThrough, Swallow, SwallowAndTapShortcutKey }

/// <summary>
/// Turns a plain press of the configured release key (desktopReleaseKey) into a tap of scrcpy's shortcut key while the
/// phone desktop window is active. The window releases its captured mouse on that key, and scrcpy only accepts Ctrl, Alt
/// or Super keys for it. Called only for events of the release key itself.
/// </summary>
public sealed class ReleaseKeyRemap
{
    private bool _held;
    private bool _swallowed;

    /// <param name="modifierHeld">Shift, Ctrl, Alt or Windows is held; such combinations reach the window unchanged.</param>
    public ReleaseKeyAction OnKey(bool down, bool modifierHeld, bool desktopActive)
    {
        if (!down)
        {
            // The release follows what happened to the press, so the window never sees half a key press
            bool swallowed = _swallowed;
            _held = false;
            _swallowed = false;
            return swallowed ? ReleaseKeyAction.Swallow : ReleaseKeyAction.PassThrough;
        }
        if (_held) return _swallowed ? ReleaseKeyAction.Swallow : ReleaseKeyAction.PassThrough; // auto-repeat follows the first press
        _held = true;
        if (!desktopActive || modifierHeld) return ReleaseKeyAction.PassThrough;
        _swallowed = true;
        return ReleaseKeyAction.SwallowAndTapShortcutKey;
    }
}
