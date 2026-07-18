using System.ComponentModel;
using System.Drawing;
using System.Windows;
using WinForms = System.Windows.Forms;
using KeyStrokes.Services;
using KeyStrokes.ViewModels;

namespace KeyStrokes;

public partial class App : Application
{
    private const string MutexName = "KeyStrokes.SingleInstance.6b1f";

    private Mutex? _instanceMutex;
    private TrackingService? _tracking;
    private MainViewModel? _viewModel;
    private MainWindow? _mainWindow;

    private WinForms.NotifyIcon? _tray;
    private WinForms.ToolStripMenuItem? _pauseResumeItem;
    private bool _explicitExit;
    private bool _firstHideBalloonShown;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single instance — a second launch just bows out.
        _instanceMutex = new Mutex(true, MutexName, out bool isNew);
        if (!isNew)
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show("An unexpected error occurred:\n\n" + args.Exception.Message,
                "KeyStrokes", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        _tracking = new TrackingService();
        _tracking.Initialize();

        _viewModel = new MainViewModel(_tracking);
        _viewModel.ExitRequested += ExitApp;
        _tracking.StateChanged += OnTrackingStateChanged;

        _mainWindow = new MainWindow { DataContext = _viewModel };
        _mainWindow.Closing += OnMainWindowClosing;

        SetupTray();

        _mainWindow.Show();
        _viewModel.StartClock();

        SessionEnding += (_, __) => _ = _tracking?.SaveNowAsync();
    }

    private void SetupTray()
    {
        Icon icon;
        try
        {
            var exe = Environment.ProcessPath ?? System.Reflection.Assembly.GetEntryAssembly()!.Location;
            icon = Icon.ExtractAssociatedIcon(exe) ?? SystemIcons.Application;
        }
        catch
        {
            icon = SystemIcons.Application;
        }

        var menu = new WinForms.ContextMenuStrip
        {
            BackColor = Color.FromArgb(26, 32, 46),
            ForeColor = Color.FromArgb(230, 234, 245),
            RenderMode = WinForms.ToolStripRenderMode.System,
        };

        var showItem = new WinForms.ToolStripMenuItem("Show KeyStrokes", null, (_, __) => ShowMainWindow());
        showItem.Font = new Font(showItem.Font, System.Drawing.FontStyle.Bold);

        _pauseResumeItem = new WinForms.ToolStripMenuItem("Pause tracking", null, (_, __) =>
        {
            if (_viewModel != null) _viewModel.IsTrackingEnabled = !_viewModel.IsTrackingEnabled;
        });

        var exitItem = new WinForms.ToolStripMenuItem("Exit", null, (_, __) => ExitApp());

        menu.Items.Add(showItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(_pauseResumeItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        _tray = new WinForms.NotifyIcon
        {
            Icon = icon,
            Visible = true,
            Text = "KeyStrokes",
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, __) => ShowMainWindow();

        UpdateTrayState();
    }

    private void OnTrackingStateChanged()
    {
        var disp = Dispatcher;
        if (!disp.CheckAccess()) disp.BeginInvoke(UpdateTrayState);
        else UpdateTrayState();
    }

    private void UpdateTrayState()
    {
        if (_tracking == null || _tray == null) return;

        bool on = _tracking.IsTrackingEnabled;
        if (_pauseResumeItem != null)
            _pauseResumeItem.Text = on ? "Pause tracking" : "Resume tracking";

        string status = !on ? "Tracking off"
            : _tracking.IsExcluded ? "Privacy pause"
            : "Recording";
        // NotifyIcon tooltip text is limited to 63 chars.
        _tray.Text = $"KeyStrokes — {status}";
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null) return;
        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = false;
    }

    private void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_explicitExit) return;

        var settings = _tracking?.Settings;
        if (settings == null || settings.MinimizeToTrayOnClose)
        {
            // Hide to tray instead of exiting.
            e.Cancel = true;
            _mainWindow?.Hide();

            if (!_firstHideBalloonShown && _tray != null)
            {
                _firstHideBalloonShown = true;
                _tray.BalloonTipTitle = "Still running";
                _tray.BalloonTipText = "KeyStrokes is tracking from the tray. Right-click the icon to pause or exit.";
                _tray.ShowBalloonTip(3000);
            }
        }
        else
        {
            ExitApp();
        }
    }

    private void ExitApp()
    {
        if (_explicitExit) return;
        _explicitExit = true;

        _viewModel?.StopClock();

        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }

        try
        {
            // Graceful commit on exit — block briefly so data is flushed.
            _tracking?.ShutdownAsync().GetAwaiter().GetResult();
        }
        catch { /* never block shutdown on a save error */ }

        _tracking?.Dispose();

        try { _mainWindow?.Close(); } catch { }

        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();

        Shutdown();
    }
}
