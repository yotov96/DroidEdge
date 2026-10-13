using System.Diagnostics;
using System.Runtime.InteropServices;
using DroidEdge.Core.Input;
using DroidEdge.Native;
using Serilog;

namespace DroidEdge.Win;

/// <summary>WH_MOUSE_LL and WH_KEYBOARD_LL on a dedicated thread. When Swallow is true, all non-injected input is blocked from Windows.</summary>
public sealed class LowLevelHooks : IInputGate, IDisposable
{
    private readonly PanicHotkey _panic;
    private readonly ILogger _log;
    private readonly User32.HookProc _mouseProc;
    private readonly User32.HookProc _keyboardProc;
    private IntPtr _mouseHook, _keyboardHook;
    private Thread? _thread;
    private uint _threadId;
    private volatile bool _swallow;
    private readonly ManualResetEventSlim _started = new();
    private Exception? _startError;
    private long _slowWarnings;
    private readonly HashSet<(uint scanCode, bool extended)> _unknownKeysLogged = new();
    private const uint LlkhfExtended = 0x01; // KBDLLHOOKSTRUCT.flags: E0-prefixed scan code
    private readonly TildeKeyRemap _tildeRemap = new();
    private volatile int _desktopProcessId;

    public LowLevelHooks(PanicHotkey panic, ILogger log)
    {
        _panic = panic;
        _log = log;
        _mouseProc = MouseProc;       // keep delegates alive for the hook's lifetime
        _keyboardProc = KeyboardProc;
    }

    public bool Swallow { get => _swallow; set => _swallow = value; }

    /// <summary>
    /// Process id of the phone desktop window's scrcpy.exe, or 0 while it is closed. While that window is active and the
    /// mouse is on the PC, the tilde key releases its captured mouse (see <see cref="TildeKeyRemap"/>).
    /// </summary>
    public int DesktopProcessId { get => _desktopProcessId; set => _desktopProcessId = value; }
    public event Action<int, int>? CursorMoved;
    public event Action? PanicPressed;
    /// <summary>A non-injected key press/release as a HID usage. Raised in every mode; the router ignores it while on the PC.</summary>
    public event Action<byte, bool>? Key;

    public void Start()
    {
        _thread = new Thread(HookThread) { Name = "ll-hooks", IsBackground = true, Priority = ThreadPriority.Highest };
        _thread.Start();
        _started.Wait();
        if (_startError is not null) throw _startError;
    }

    private void HookThread()
    {
        try
        {
            _threadId = User32.GetCurrentThreadId();
            var hMod = User32.GetModuleHandleW(null);
            _mouseHook = User32.SetWindowsHookExW(User32.WH_MOUSE_LL, _mouseProc, hMod, 0);
            if (_mouseHook == IntPtr.Zero) throw new InvalidOperationException($"WH_MOUSE_LL failed: {Marshal.GetLastWin32Error()}");
            _keyboardHook = User32.SetWindowsHookExW(User32.WH_KEYBOARD_LL, _keyboardProc, hMod, 0);
            if (_keyboardHook == IntPtr.Zero) throw new InvalidOperationException($"WH_KEYBOARD_LL failed: {Marshal.GetLastWin32Error()}");
        }
        catch (Exception ex)
        {
            _startError = ex;
            _started.Set();
            return;
        }
        _started.Set();
        while (User32.GetMessageW(out var msg, IntPtr.Zero, 0, 0))
        {
            User32.TranslateMessage(ref msg);
            User32.DispatchMessageW(ref msg);
        }
        if (_mouseHook != IntPtr.Zero) User32.UnhookWindowsHookEx(_mouseHook);
        if (_keyboardHook != IntPtr.Zero) User32.UnhookWindowsHookEx(_keyboardHook);
    }

    private IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode != User32.HC_ACTION) return User32.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        long t0 = Stopwatch.GetTimestamp();
        var info = Marshal.PtrToStructure<User32.MSLLHOOKSTRUCT>(lParam);
        bool injected = (info.flags & User32.LLMHF_INJECTED) != 0;
        IntPtr result;
        if (injected)
        {
            result = User32.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }
        else if (_swallow)
        {
            result = 1;
        }
        else
        {
            if ((int)wParam == User32.WM_MOUSEMOVE) CursorMoved?.Invoke(info.pt.X, info.pt.Y);
            result = User32.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }
        WarnIfSlow(t0, "mouse");
        return result;
    }

    private IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode != User32.HC_ACTION) return User32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        long t0 = Stopwatch.GetTimestamp();
        var info = Marshal.PtrToStructure<User32.KBDLLHOOKSTRUCT>(lParam);
        bool injected = (info.flags & User32.LLKHF_INJECTED) != 0;
        int msg = (int)wParam;
        bool down = msg is User32.WM_KEYDOWN or User32.WM_SYSKEYDOWN;
        if (!injected)
        {
            if (_panic.OnKey((int)info.vkCode, down))
            {
                _log.Information("Panic hotkey pressed");
                PanicPressed?.Invoke();
            }
            bool extended = (info.flags & LlkhfExtended) != 0;
            if (info.scanCode == TildeKeyRemap.ScanCode && !extended)
            {
                TildeKeyAction action = _tildeRemap.OnKey((ushort)info.scanCode, extended, down, IsModifierHeld(), !_swallow && IsDesktopActive());
                if (action != TildeKeyAction.PassThrough)
                {
                    if (action == TildeKeyAction.SwallowAndTapRightControl) TapRightControl();
                    WarnIfSlow(t0, "keyboard");
                    return 1;
                }
            }
            if (KeyDecoder.TryDecode((ushort)info.scanCode, extended, info.vkCode, out var usage))
            {
                Key?.Invoke(usage, down);
            }
            else if (_unknownKeysLogged.Add((info.scanCode, extended)))
            {
                _log.Debug("Unmapped scancode 0x{Make:X2} extended={Ext} vk=0x{Vk:X2}", info.scanCode, extended, info.vkCode);
            }
        }
        IntPtr result = !injected && _swallow ? 1 : User32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        WarnIfSlow(t0, "keyboard");
        return result;
    }

    private bool IsDesktopActive()
    {
        int processId = _desktopProcessId;
        if (processId == 0) return false;
        IntPtr window = User32.GetForegroundWindow();
        if (window == IntPtr.Zero) return false;
        User32.GetWindowThreadProcessId(window, out uint foregroundProcessId);
        return foregroundProcessId == (uint)processId;
    }

    private static readonly int[] ModifierKeys = [User32.VK_SHIFT, User32.VK_CONTROL, User32.VK_MENU, User32.VK_LWIN, User32.VK_RWIN];

    private static bool IsModifierHeld()
    {
        foreach (int vk in ModifierKeys)
        {
            if ((User32.GetAsyncKeyState(vk) & 0x8000) != 0) return true;
        }
        return false;
    }

    private void TapRightControl()
    {
        // The scan code goes in explicitly: SDL (scrcpy's window) tells Right Ctrl from Left Ctrl by scan code and E0 flag
        User32.INPUT[] inputs =
        [
            new User32.INPUT { type = User32.INPUT_KEYBOARD, u = new User32.INPUTUNION { ki = new User32.KEYBDINPUT { wVk = User32.VK_RCONTROL, wScan = User32.SCAN_CONTROL, dwFlags = User32.KEYEVENTF_EXTENDEDKEY } } },
            new User32.INPUT { type = User32.INPUT_KEYBOARD, u = new User32.INPUTUNION { ki = new User32.KEYBDINPUT { wVk = User32.VK_RCONTROL, wScan = User32.SCAN_CONTROL, dwFlags = User32.KEYEVENTF_EXTENDEDKEY | User32.KEYEVENTF_KEYUP } } },
        ];
        uint sent = User32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<User32.INPUT>());
        if (sent != inputs.Length) _log.Warning("Sending Right Ctrl for the tilde key failed: {Error}", Marshal.GetLastWin32Error());
    }

    private void WarnIfSlow(long t0, string which)
    {
        var elapsed = Stopwatch.GetElapsedTime(t0);
        if (elapsed.TotalMilliseconds > 5 && Interlocked.Increment(ref _slowWarnings) <= 20)
            _log.Warning("{Hook} hook callback took {Ms:0.0} ms (swallow={Swallow})", which, elapsed.TotalMilliseconds, _swallow);
    }

    public void Stop()
    {
        if (_thread is null) return;
        User32.PostThreadMessageW(_threadId, User32.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(2000);
        _thread = null;
    }

    public void Dispose()
    {
        _swallow = false;
        Stop();
        _started.Dispose();
    }
}
