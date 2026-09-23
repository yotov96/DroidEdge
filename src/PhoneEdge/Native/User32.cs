using System.Runtime.InteropServices;

namespace PhoneEdge.Native;

internal static class User32
{
    public const int WH_KEYBOARD_LL = 13;
    public const int WH_MOUSE_LL = 14;
    public const int HC_ACTION = 0;
    public const int WM_INPUT = 0x00FF;
    public const int WM_MOUSEMOVE = 0x0200;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP = 0x0105;
    public const uint LLMHF_INJECTED = 0x01;
    public const uint LLKHF_INJECTED = 0x10;
    public const uint RIDEV_INPUTSINK = 0x00000100;
    public const uint RID_INPUT = 0x10000003;
    public const uint RIM_TYPEMOUSE = 0;
    public const uint RIM_TYPEKEYBOARD = 1;
    public const ushort MOUSE_MOVE_ABSOLUTE = 0x01;
    public const ushort RI_KEY_BREAK = 0x01;
    public const ushort RI_KEY_E0 = 0x02;
    public const ushort RI_KEY_E1 = 0x04;
    public const int VK_PAUSE = 0x13;
    public static readonly IntPtr HWND_MESSAGE = new(-3);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData; public uint flags; public uint time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT { public uint vkCode; public uint scanCode; public uint flags; public uint time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWINPUTDEVICE { public ushort usUsagePage; public ushort usUsage; public uint dwFlags; public IntPtr hwndTarget; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWINPUTHEADER { public uint dwType; public uint dwSize; public IntPtr hDevice; public IntPtr wParam; }

    [StructLayout(LayoutKind.Explicit)]
    public struct RAWMOUSE
    {
        [FieldOffset(0)] public ushort usFlags;
        [FieldOffset(4)] public ushort usButtonFlags;
        [FieldOffset(6)] public ushort usButtonData;
        [FieldOffset(8)] public uint ulRawButtons;
        [FieldOffset(12)] public int lLastX;
        [FieldOffset(16)] public int lLastY;
        [FieldOffset(20)] public uint ulExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWKEYBOARD { public ushort MakeCode; public ushort Flags; public ushort Reserved; public ushort VKey; public uint Message; public uint ExtraInformation; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MONITORINFO { public uint cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

    public const uint MONITORINFOF_PRIMARY = 1;

    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
    public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetWindowsHookExW(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterRawInputDevices([In] RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);
    [DllImport("user32.dll")] public static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetCursorPos(int x, int y);
    public const int SW_RESTORE = 9;
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MONITORINFO lpmi);
    [DllImport("user32.dll")] public static extern bool GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
    [DllImport("user32.dll")] public static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] public static extern IntPtr DispatchMessageW(ref MSG lpMsg);
    [DllImport("user32.dll")] public static extern bool PostThreadMessageW(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandleW(string? lpModuleName);

    public const uint WM_QUIT = 0x0012;

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public POINT pt; }
}
