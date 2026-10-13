using DroidEdge.Core.Input;
using DroidEdge.Native;

namespace DroidEdge.Win;

public sealed class Win32Cursor : ICursor
{
    public (int x, int y) GetPosition() => User32.GetCursorPos(out var p) ? (p.X, p.Y) : (0, 0);
    public void SetPosition(int x, int y) => User32.SetCursorPos(x, y);
}
