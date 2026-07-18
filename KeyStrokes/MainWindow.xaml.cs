using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using KeyStrokes.Interop;

namespace KeyStrokes;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        StateChanged += (_, __) => UpdateMaxButtonGlyph();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        ApplyModernWindowStyle(hwnd);

        var source = HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);
    }

    private void ApplyModernWindowStyle(IntPtr hwnd)
    {
        // Dark titlebar
        int dark = 1;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

        // Rounded corners
        int round = NativeMethods.DWMWCP_ROUND;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));

        // Mica backdrop (Windows 11). If it can't be applied, fall back to a solid dark
        // window so the desktop never shows through the transparent background.
        int backdrop = NativeMethods.DWMSBT_MAINWINDOW;
        int hr = NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
        if (hr != 0)
        {
            Background = (System.Windows.Media.Brush)FindResource("BgBaseBrush");
        }
    }

    // Constrain a maximized borderless window to the monitor work area so it neither
    // overshoots the screen edges nor covers the taskbar.
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_GETMINMAXINFO)
        {
            var mmi = Marshal.PtrToStructure<NativeMethods.MINMAXINFO>(lParam);
            IntPtr monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                var mi = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
                if (NativeMethods.GetMonitorInfo(monitor, ref mi))
                {
                    var work = mi.rcWork;
                    var bounds = mi.rcMonitor;
                    mmi.ptMaxPosition.X = work.Left - bounds.Left;
                    mmi.ptMaxPosition.Y = work.Top - bounds.Top;
                    mmi.ptMaxSize.X = work.Right - work.Left;
                    mmi.ptMaxSize.Y = work.Bottom - work.Top;
                    mmi.ptMinTrackSize.X = (int)MinWidth;
                    mmi.ptMinTrackSize.Y = (int)MinHeight;
                    Marshal.StructureToPtr(mmi, lParam, true);
                }
            }
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void UpdateMaxButtonGlyph()
    {
        // E922 = maximize, E923 = restore
        MaxButton.Content = WindowState == WindowState.Maximized ? "" : "";
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeRestore(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
