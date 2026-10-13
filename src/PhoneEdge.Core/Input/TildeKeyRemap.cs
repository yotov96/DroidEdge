namespace PhoneEdge.Core.Input;

public enum TildeKeyAction { PassThrough, Swallow, SwallowAndTapRightControl }

/// <summary>
/// Turns a plain press of the tilde key into a Right Ctrl tap while the phone desktop window is active. The window
/// releases its captured mouse on Right Ctrl, and scrcpy only accepts Ctrl, Alt or Super keys for that.
/// </summary>
public sealed class TildeKeyRemap
{
    /// <summary>Scan code of the key left of 1 (` and ~ on a US layout); the same physical key on any layout.</summary>
    public const ushort ScanCode = 0x29;

    private bool _held;
    private bool _swallowed;

    /// <param name="modifierHeld">Shift, Ctrl, Alt or Windows is held; such combinations reach the window unchanged.</param>
    public TildeKeyAction OnKey(ushort scanCode, bool extended, bool down, bool modifierHeld, bool desktopActive)
    {
        if (scanCode != ScanCode || extended) return TildeKeyAction.PassThrough;
        if (!down)
        {
            // The release follows what happened to the press, so the window never sees half a key press
            bool swallowed = _swallowed;
            _held = false;
            _swallowed = false;
            return swallowed ? TildeKeyAction.Swallow : TildeKeyAction.PassThrough;
        }
        if (_held) return _swallowed ? TildeKeyAction.Swallow : TildeKeyAction.PassThrough; // auto-repeat follows the first press
        _held = true;
        if (!desktopActive || modifierHeld) return TildeKeyAction.PassThrough;
        _swallowed = true;
        return TildeKeyAction.SwallowAndTapRightControl;
    }
}
