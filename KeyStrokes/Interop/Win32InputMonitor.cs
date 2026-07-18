using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace KeyStrokes.Interop;

/// <summary>
/// Owns the global input plumbing on a single dedicated background thread with
/// its own Win32 message pump. That isolation is what keeps typing/gaming input
/// latency at zero: the low-level keyboard callback returns almost immediately
/// (it only reads a couple of volatile flags and raises a lightweight event),
/// and it is never blocked behind the WPF UI thread.
///
/// The class raises two events, both fired on the monitor thread:
///   * <see cref="KeyDown"/>          — a virtual-key code was pressed.
///   * <see cref="ForegroundChanged"/> — the focused top-level window changed.
///
/// It never reconstructs typed text; only individual virtual-key codes cross the
/// boundary, and consumers aggregate them into per-key counts.
/// </summary>
internal sealed class Win32InputMonitor : IDisposable
{
    // Kept as fields so the GC never collects the unmanaged callback thunks.
    private readonly NativeMethods.LowLevelKeyboardProc _keyboardProc;
    private readonly NativeMethods.LowLevelMouseProc _mouseProc;
    private readonly NativeMethods.WinEventDelegate _winEventProc;

    private readonly HashSet<int> _pressedKeys = new();
    private NativeMethods.POINT _lastMousePos;
    private bool _hasLastMousePos;

    private IntPtr _keyboardHook = IntPtr.Zero;
    private IntPtr _mouseHook = IntPtr.Zero;
    private IntPtr _winEventHook = IntPtr.Zero;
    private Thread? _thread;
    private uint _threadId;
    private volatile bool _running;

    /// <summary>Master gate. When false the callback short-circuits instantly.</summary>
    public volatile bool CaptureEnabled;

    public event Action<int>? KeyDown;
    public event Action<double>? MouseMoved;
    public event Action<double>? MouseScrolled;
    public event Action<ForegroundInfo>? ForegroundChanged;

    public Win32InputMonitor()
    {
        _keyboardProc = KeyboardCallback;
        _mouseProc = MouseCallback;
        _winEventProc = WinEventCallback;
    }

    public bool IsRunning => _running;

    /// <summary>Spin up the dedicated thread and install both hooks.</summary>
    public void Start()
    {
        if (_running) return;
        _running = true;

        _thread = new Thread(ThreadProc)
        {
            IsBackground = true,
            Name = "KeyStrokes.InputMonitor",
            Priority = ThreadPriority.AboveNormal,
        };
        // STA is not required for hooks, but keeps us well-behaved w.r.t. any
        // shell interactions and matches the single-message-pump model.
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    private void ThreadProc()
    {
        _pressedKeys.Clear();
        _hasLastMousePos = false;

        IntPtr hInstance = NativeMethods.GetModuleHandle(null);

        _keyboardHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL, _keyboardProc, hInstance, 0);

        _mouseHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL, _mouseProc, hInstance, 0);

        _winEventHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _winEventProc, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);

        _threadId = NativeMethods.GetCurrentThreadId();

        // Report the initial foreground window so exclusions apply immediately.
        RaiseForeground(NativeMethods.GetForegroundWindow());

        // Classic Win32 message loop. GetMessage blocks efficiently (0% CPU)
        // until a hook callback, a WinEvent, or WM_QUIT arrives.
        while (_running &&
               NativeMethods.GetMessage(out NativeMethods.MSG msg, IntPtr.Zero, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }

        if (_keyboardHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
        if (_mouseHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
        if (_winEventHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_winEventHook);
            _winEventHook = IntPtr.Zero;
        }
    }

    private IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // HC_ACTION (0) means lParam holds a valid MSLLHOOKSTRUCT.
        if (nCode == 0 && CaptureEnabled)
        {
            int msg = (int)wParam;
            if (msg == NativeMethods.WM_MOUSEMOVE)
            {
                var data = System.Runtime.InteropServices.Marshal
                    .PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                if (_hasLastMousePos)
                {
                    double dx = data.pt.X - _lastMousePos.X;
                    double dy = data.pt.Y - _lastMousePos.Y;
                    double distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance > 0)
                    {
                        MouseMoved?.Invoke(distance);
                    }
                }
                _lastMousePos = data.pt;
                _hasLastMousePos = true;
            }
            else if (msg == NativeMethods.WM_LBUTTONDOWN)
            {
                KeyDown?.Invoke(0x01); // VK_LBUTTON
            }
            else if (msg == NativeMethods.WM_RBUTTONDOWN)
            {
                KeyDown?.Invoke(0x02); // VK_RBUTTON
            }
            else if (msg == NativeMethods.WM_MBUTTONDOWN)
            {
                KeyDown?.Invoke(0x04); // VK_MBUTTON
            }
            else if (msg == NativeMethods.WM_XBUTTONDOWN)
            {
                var data = System.Runtime.InteropServices.Marshal
                    .PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                int xbutton = (int)((data.mouseData >> 16) & 0xFFFF);
                if (xbutton == 1)
                {
                    KeyDown?.Invoke(0x05); // VK_XBUTTON1
                }
                else if (xbutton == 2)
                {
                    KeyDown?.Invoke(0x06); // VK_XBUTTON2
                }
            }
            else if (msg == NativeMethods.WM_MOUSEWHEEL)
            {
                var data = System.Runtime.InteropServices.Marshal
                    .PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                short delta = (short)((data.mouseData >> 16) & 0xFFFF);
                if (delta > 0)
                {
                    KeyDown?.Invoke(0x101); // Pseudo-VK for Scroll Wheel Up
                }
                else if (delta < 0)
                {
                    KeyDown?.Invoke(0x102); // Pseudo-VK for Scroll Wheel Down
                }
                MouseScrolled?.Invoke(Math.Abs(delta));
            }
            else if (msg == NativeMethods.WM_MOUSEHWHEEL)
            {
                var data = System.Runtime.InteropServices.Marshal
                    .PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                short delta = (short)((data.mouseData >> 16) & 0xFFFF);
                if (delta > 0)
                {
                    KeyDown?.Invoke(0x104); // Pseudo-VK for Scroll Wheel Right
                }
                else if (delta < 0)
                {
                    KeyDown?.Invoke(0x103); // Pseudo-VK for Scroll Wheel Left
                }
                MouseScrolled?.Invoke(Math.Abs(delta));
            }
        }
        return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private IntPtr KeyboardCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // HC_ACTION (0) means lParam holds a valid KBDLLHOOKSTRUCT.
        if (nCode == 0 && CaptureEnabled)
        {
            int msg = (int)wParam;
            if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
            {
                var data = System.Runtime.InteropServices.Marshal
                    .PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                int vk = (int)data.vkCode;
                if (_pressedKeys.Add(vk))
                {
                    // Fire and forget — the handler must be O(1). We never hold the
                    // hook chain hostage while counting happens.
                    KeyDown?.Invoke(vk);
                }
            }
            else if (msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP)
            {
                var data = System.Runtime.InteropServices.Marshal
                    .PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                int vk = (int)data.vkCode;
                _pressedKeys.Remove(vk);
            }
        }
        return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        RaiseForeground(hwnd);
    }

    private void RaiseForeground(IntPtr hwnd)
    {
        var handler = ForegroundChanged;
        if (handler == null) return;
        handler(BuildForegroundInfo(hwnd));
    }

    private static ForegroundInfo BuildForegroundInfo(IntPtr hwnd)
    {
        string process = string.Empty;
        string title = string.Empty;

        try
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid != 0)
            {
                using var p = Process.GetProcessById((int)pid);
                process = p.ProcessName;
            }
        }
        catch { /* process may have exited; treat as unknown */ }

        try
        {
            int len = NativeMethods.GetWindowTextLength(hwnd);
            if (len > 0)
            {
                var sb = new StringBuilder(len + 1);
                NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
                title = sb.ToString();
            }
        }
        catch { /* ignore */ }

        return new ForegroundInfo(hwnd, process, title);
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        CaptureEnabled = false;

        if (_threadId != 0)
        {
            // Break the GetMessage loop so the thread unwinds and unhooks cleanly.
            NativeMethods.PostThreadMessage(_threadId, NativeMethods.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }

        _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
        _threadId = 0;
    }

    public void Dispose() => Stop();
}

/// <summary>Snapshot of the currently focused top-level window.</summary>
public readonly record struct ForegroundInfo(IntPtr Handle, string ProcessName, string Title);
