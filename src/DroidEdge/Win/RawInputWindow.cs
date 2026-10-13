using System.Runtime.InteropServices;
using DroidEdge.Core.Hid;
using DroidEdge.Native;
using Serilog;

namespace DroidEdge.Win;

/// <summary>
/// Message-only window that receives WM_INPUT for all mice (RIDEV_INPUTSINK) and raises typed events.
/// Keyboards are deliberately not registered here: a key swallowed by the low-level keyboard hook never
/// produces WM_INPUT, so keystrokes are taken from the hook itself (see LowLevelHooks.Key).
/// </summary>
public sealed class RawInputWindow : NativeWindow, IDisposable
{
    private readonly ILogger _log;
    private readonly byte[] _buffer = new byte[256];
    private readonly GCHandle _pin;

    public event Action<int, int>? MouseMoved;
    public event Action<HidMouseButtons, bool>? MouseButton;
    public event Action<int, bool>? Wheel;

    public RawInputWindow(ILogger log)
    {
        _log = log;
        _pin = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
        CreateHandle(new CreateParams { Parent = User32.HWND_MESSAGE, Caption = "DroidEdgeRawInput" });
        var devices = new[]
        {
            new User32.RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x02, dwFlags = User32.RIDEV_INPUTSINK, hwndTarget = Handle }, // mouse
        };
        if (!User32.RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<User32.RAWINPUTDEVICE>()))
            throw new InvalidOperationException($"RegisterRawInputDevices failed: {Marshal.GetLastWin32Error()}");
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == User32.WM_INPUT)
        {
            try { HandleInput(m.LParam); }
            catch (Exception ex) { _log.Error(ex, "Raw input handler failed"); }
        }
        base.WndProc(ref m);
    }

    private void HandleInput(IntPtr hRawInput)
    {
        uint size = (uint)_buffer.Length;
        uint headerSize = (uint)Marshal.SizeOf<User32.RAWINPUTHEADER>();
        IntPtr p = _pin.AddrOfPinnedObject();
        if (User32.GetRawInputData(hRawInput, User32.RID_INPUT, p, ref size, headerSize) == unchecked((uint)-1)) return;
        var header = Marshal.PtrToStructure<User32.RAWINPUTHEADER>(p);
        IntPtr data = p + (int)headerSize;
        if (header.dwType == User32.RIM_TYPEMOUSE)
        {
            var mouse = Marshal.PtrToStructure<User32.RAWMOUSE>(data);
            if ((mouse.usFlags & User32.MOUSE_MOVE_ABSOLUTE) == 0 && (mouse.lLastX != 0 || mouse.lLastY != 0))
                MouseMoved?.Invoke(mouse.lLastX, mouse.lLastY);
            ushort f = mouse.usButtonFlags;
            if ((f & 0x0001) != 0) MouseButton?.Invoke(HidMouseButtons.Left, true);
            if ((f & 0x0002) != 0) MouseButton?.Invoke(HidMouseButtons.Left, false);
            if ((f & 0x0004) != 0) MouseButton?.Invoke(HidMouseButtons.Right, true);
            if ((f & 0x0008) != 0) MouseButton?.Invoke(HidMouseButtons.Right, false);
            if ((f & 0x0010) != 0) MouseButton?.Invoke(HidMouseButtons.Middle, true);
            if ((f & 0x0020) != 0) MouseButton?.Invoke(HidMouseButtons.Middle, false);
            if ((f & 0x0040) != 0) MouseButton?.Invoke(HidMouseButtons.Button4, true);
            if ((f & 0x0080) != 0) MouseButton?.Invoke(HidMouseButtons.Button4, false);
            if ((f & 0x0100) != 0) MouseButton?.Invoke(HidMouseButtons.Button5, true);
            if ((f & 0x0200) != 0) MouseButton?.Invoke(HidMouseButtons.Button5, false);
            if ((f & 0x0400) != 0) Wheel?.Invoke((short)mouse.usButtonData, false);
            if ((f & 0x0800) != 0) Wheel?.Invoke((short)mouse.usButtonData, true);
        }
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero) DestroyHandle();
        if (_pin.IsAllocated) _pin.Free();
    }
}
