using System.Collections.ObjectModel;
using KeyStrokes.Helpers;
using KeyStrokes.Models;
using KeyStrokes.Services;

namespace KeyStrokes.ViewModels;

public sealed class HeatmapViewModel : PageViewModel
{
    private readonly Dictionary<int, HeatKey> _byVk = new();

    public HeatmapViewModel(TrackingService tracking) : base(tracking)
    {
        var rows = new List<HeatRow>();
        foreach (var rowDef in KeyMapper.KeyboardLayout)
        {
            var keys = new List<HeatKey>();
            foreach (var def in rowDef)
            {
                var key = new HeatKey(def);
                keys.Add(key);
                if (!def.IsSpacer) _byVk[def.Vk] = key;
            }
            rows.Add(new HeatRow(keys));
        }
        Rows = new ObservableCollection<HeatRow>(rows);
    }

    public override string Title => "Heatmap";
    public override string Subtitle => "Where your fingers live.";
    public override string Glyph => "\uE765";
    public override int RefreshEveryTicks => 4; // ~1.25 Hz

    public ObservableCollection<HeatRow> Rows { get; }

    public TrackingService.Scope[] Scopes { get; } =
        { TrackingService.Scope.AllTime, TrackingService.Scope.Today, TrackingService.Scope.Session };

    private TrackingService.Scope _selectedScope = TrackingService.Scope.AllTime;
    public TrackingService.Scope SelectedScope
    {
        get => _selectedScope;
        set { if (SetProperty(ref _selectedScope, value)) Refresh(); }
    }

    private string _hottestKey = "—";
    public string HottestKey { get => _hottestKey; set => SetProperty(ref _hottestKey, value); }

    private long _hottestCount;
    public long HottestCount { get => _hottestCount; set => SetProperty(ref _hottestCount, value); }

    private long _totalCount;
    public long TotalCount { get => _totalCount; set => SetProperty(ref _totalCount, value); }

    public override void Refresh()
    {
        var counts = Tracking.GetCounts(SelectedScope);

        long max = 0, total = 0;
        int hottestVk = -1;
        foreach (var kv in counts)
        {
            total += kv.Value;
            if (kv.Value > max) { max = kv.Value; hottestVk = kv.Key; }
        }
        TotalCount = total;
        HottestKey = hottestVk >= 0 ? KeyMapper.FriendlyName(hottestVk) : "—";
        HottestCount = max;

        // Use a mild gamma so mid-frequency keys still read as warm.
        foreach (var kv in _byVk)
        {
            long c = counts.TryGetValue(kv.Key, out var v) ? v : 0;
            double norm = max > 0 ? (double)c / max : 0;
            double intensity = Math.Pow(norm, 0.55);

            var key = kv.Value;
            key.Count = c;
            key.Intensity = intensity;
            key.Fill = ColorUtil.HeatBrush(c > 0 ? intensity : 0);
        }
    }
}
