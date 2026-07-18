using System.Diagnostics;
using System.Windows.Input;
using KeyStrokes.Helpers;
using KeyStrokes.Models;
using KeyStrokes.Services;

namespace KeyStrokes.ViewModels;

public sealed class SettingsViewModel : PageViewModel
{
    public SettingsViewModel(TrackingService tracking) : base(tracking)
    {
        _excludedProcessesText = string.Join(Environment.NewLine, tracking.Settings.ExcludedProcesses);
        _excludedTitleKeywordsText = string.Join(Environment.NewLine, tracking.Settings.ExcludedTitleKeywords);
        _minimizeToTray = tracking.Settings.MinimizeToTrayOnClose;

        SaveCommand = new RelayCommand(SavePrivacy);
        OpenDataFolderCommand = new RelayCommand(OpenDataFolder);
    }

    public override string Title => "Settings";
    public override string Subtitle => "Privacy, exclusions & data.";
    public override string Glyph => "\uE713";
    public override int RefreshEveryTicks => int.MaxValue; // static page — never auto-polls

    public ICommand SaveCommand { get; }
    public ICommand OpenDataFolderCommand { get; }

    public string DataFolderPath => Tracking.Storage.DataDirectory;
    public string DataFilePath => Tracking.Storage.DataFilePath;

    private string _excludedProcessesText;
    public string ExcludedProcessesText
    {
        get => _excludedProcessesText;
        set => SetProperty(ref _excludedProcessesText, value);
    }

    private string _excludedTitleKeywordsText;
    public string ExcludedTitleKeywordsText
    {
        get => _excludedTitleKeywordsText;
        set => SetProperty(ref _excludedTitleKeywordsText, value);
    }

    private bool _minimizeToTray;
    public bool MinimizeToTrayOnClose
    {
        get => _minimizeToTray;
        set
        {
            if (SetProperty(ref _minimizeToTray, value))
            {
                Tracking.Settings.MinimizeToTrayOnClose = value;
                _ = Tracking.SaveNowAsync();
            }
        }
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    private void SavePrivacy(object? _)
    {
        var settings = new AppSettings
        {
            ExcludedProcesses = SplitLines(ExcludedProcessesText),
            ExcludedTitleKeywords = SplitLines(ExcludedTitleKeywordsText),
        };
        Tracking.UpdatePrivacyRules(settings);
        _ = Tracking.SaveNowAsync();
        StatusMessage = $"Saved — {settings.ExcludedProcesses.Count} apps, {settings.ExcludedTitleKeywords.Count} keywords protected.";
    }

    private void OpenDataFolder(object? _)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Tracking.Storage.DataDirectory,
                UseShellExecute = true,
            });
        }
        catch { /* folder may not exist yet; ignore */ }
    }

    private static List<string> SplitLines(string text) => text
        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(s => s.Trim())
        .Where(s => s.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}
