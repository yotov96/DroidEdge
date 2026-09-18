using System.Diagnostics;
using System.Runtime.InteropServices;
using PhoneEdge.Core.Input;
using PhoneEdge.Native;
using Serilog;

namespace PhoneEdge.Win;

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

    public LowLevelHooks(PanicHotkey panic, ILogger log)
    {
        _panic = panic;
        _log = log;
        _mouseProc = MouseProc;       // keep delegates alive for the hook's lifetime
        _keyboardProc = KeyboardProc;
    }

    public bool Swallow { get => _swallow; set => _swallow = value; }
    public event Action<int, int>? CursorMoved;
    public event Action? PanicPressed;

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
        if (!injected && _panic.OnKey((int)info.vkCode, down))
        {
            _log.Information("Panic hotkey pressed");
            PanicPressed?.Invoke();
        }
        IntPtr result = !injected && _swallow ? 1 : User32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        WarnIfSlow(t0, "keyboard");
        return result;
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
