using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using KeyStrokes.Helpers;
using KeyStrokes.Models;
using KeyStrokes.Services;

namespace KeyStrokes.ViewModels;

/// <summary>The shell view model: navigation, the master toggle, live status,
/// the polling clock that drives all live updates, and the data-management commands.</summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly TrackingService _tracking;
    private readonly DispatcherTimer _clock;
    private long _tickCount;

    public MainViewModel(TrackingService tracking)
    {
        _tracking = tracking;

        Dashboard = new DashboardViewModel(tracking);
        Breakdown = new BreakdownViewModel(tracking);
        Heatmap = new HeatmapViewModel(tracking);
        History = new HistoryViewModel(tracking);
        Settings = new SettingsViewModel(tracking);

        Pages = new ObservableCollection<PageViewModel> { Dashboard, Breakdown, Heatmap, History, Settings };

        NavigateCommand = new RelayCommand(p => { if (p is PageViewModel vm) CurrentPage = vm; });
        ToggleTrackingCommand = new RelayCommand(() => IsTrackingEnabled = !IsTrackingEnabled);
        ClearSessionCommand = new RelayCommand(ClearSession);
        ClearAllCommand = new RelayCommand(ClearAll);
        ExportCsvCommand = new RelayCommand(ExportCsv);
        ExportJsonCommand = new RelayCommand(ExportJson);
        ExitCommand = new RelayCommand(() => ExitRequested?.Invoke());

        _currentPage = Dashboard;
        _currentPage.IsSelected = true;

        _tracking.StateChanged += OnServiceStateChanged;
        SyncStatus();

        _clock = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(200) };
        _clock.Tick += OnTick;
    }

    // ---- Pages / navigation ---------------------------------------------
    public ObservableCollection<PageViewModel> Pages { get; }
    public DashboardViewModel Dashboard { get; }
    public BreakdownViewModel Breakdown { get; }
    public HeatmapViewModel Heatmap { get; }
    public HistoryViewModel History { get; }
    public SettingsViewModel Settings { get; }

    private PageViewModel _currentPage;
    public PageViewModel CurrentPage
    {
        get => _currentPage;
        set
        {
            if (_currentPage == value) return;
            _currentPage.IsSelected = false;
            _currentPage = value;
            _currentPage.IsSelected = true;
            _tickCount = 0;
            OnPropertyChanged();
            _currentPage.OnActivated();
        }
    }

    public ICommand NavigateCommand { get; }
    public ICommand ToggleTrackingCommand { get; }
    public ICommand ClearSessionCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand ExportCsvCommand { get; }
    public ICommand ExportJsonCommand { get; }
    public ICommand ExitCommand { get; }

    public event Action? ExitRequested;

    // ---- Master toggle & status -----------------------------------------
    public bool IsTrackingEnabled
    {
        get => _tracking.IsTrackingEnabled;
        set
        {
            if (_tracking.IsTrackingEnabled == value) return;
            _tracking.SetTrackingEnabled(value);
            OnPropertyChanged();
            SyncStatus();
        }
    }

    private string _statusText = string.Empty;
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

    private string _statusDetail = string.Empty;
    public string StatusDetail { get => _statusDetail; set => SetProperty(ref _statusDetail, value); }

    // "active" | "off" | "excluded" — drives the status pill styling in XAML.
    private string _statusKind = "off";
    public string StatusKind { get => _statusKind; set => SetProperty(ref _statusKind, value); }

    private void OnServiceStateChanged()
    {
        // May arrive on the monitor thread — marshal to the UI thread.
        var disp = Application.Current?.Dispatcher;
        if (disp != null && !disp.CheckAccess())
            disp.BeginInvoke(SyncStatus);
        else
            SyncStatus();
    }

    private void SyncStatus()
    {
        OnPropertyChanged(nameof(IsTrackingEnabled));

        if (!_tracking.IsTrackingEnabled)
        {
            StatusText = "Tracking off";
            StatusDetail = "The keyboard hook is removed — zero capture, zero CPU.";
            StatusKind = "off";
        }
        else if (_tracking.IsExcluded)
        {
            StatusText = "Privacy pause";
            StatusDetail = string.IsNullOrEmpty(_tracking.ExclusionReason)
                ? "A protected app is focused — keystrokes are being discarded."
                : _tracking.ExclusionReason;
            StatusKind = "excluded";
        }
        else
        {
            StatusText = "Recording";
            StatusDetail = "Capturing global keystrokes locally.";
            StatusKind = "active";
        }
    }

    // ---- Live clock ------------------------------------------------------
    public void StartClock() => _clock.Start();
    public void StopClock() => _clock.Stop();

    private void OnTick(object? sender, EventArgs e)
    {
        _tickCount++;
        SyncStatus();

        int every = Math.Max(1, CurrentPage.RefreshEveryTicks);
        if (_tickCount % every == 0)
            CurrentPage.Refresh();
    }

    // ---- Data management -------------------------------------------------
    private void ClearSession(object? _)
    {
        _tracking.ClearSession();
        CurrentPage.Refresh();
    }

    private async void ClearAll(object? _)
    {
        var result = MessageBox.Show(
            "Permanently erase all recorded data — lifetime totals, every key count, and full history?\n\nThis cannot be undone.",
            "Clear all data",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        if (result != MessageBoxResult.Yes) return;

        await _tracking.ClearAllDataAsync();
        foreach (var p in Pages) p.Refresh();
    }

    private async void ExportCsv(object? _)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export key breakdown",
            Filter = "CSV file (*.csv)|*.csv",
            FileName = $"KeyStrokes-breakdown-{DateTime.Now:yyyy-MM-dd}.csv",
        };
        if (dlg.ShowDialog() != true) return;

        var counts = _tracking.GetCounts(TrackingService.Scope.AllTime);
        long total = counts.Values.Sum();
        var stats = counts.Select(kv => new KeyStat(kv.Key, kv.Value)
        {
            Percentage = total > 0 ? kv.Value * 100.0 / total : 0,
        });

        try
        {
            await ExportService.ExportKeyBreakdownCsvAsync(dlg.FileName, stats);
            MessageBox.Show($"Exported to:\n{dlg.FileName}", "Export complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", "Export", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ExportJson(object? _)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export full data",
            Filter = "JSON file (*.json)|*.json",
            FileName = $"KeyStrokes-data-{DateTime.Now:yyyy-MM-dd}.json",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            await ExportService.ExportFullJsonAsync(dlg.FileName, _tracking.GetDataSnapshot());
            MessageBox.Show($"Exported to:\n{dlg.FileName}", "Export complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", "Export", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
