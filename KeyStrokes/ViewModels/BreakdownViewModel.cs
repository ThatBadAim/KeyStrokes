using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using KeyStrokes.Models;
using KeyStrokes.Services;

namespace KeyStrokes.ViewModels;

public sealed class BreakdownViewModel : PageViewModel
{
    private readonly Dictionary<int, KeyStat> _byVk = new();
    private readonly ObservableCollection<KeyStat> _stats = new();

    public BreakdownViewModel(TrackingService tracking) : base(tracking)
    {
        StatsView = CollectionViewSource.GetDefaultView(_stats);
        StatsView.Filter = FilterPredicate;
        StatsView.SortDescriptions.Add(new SortDescription(nameof(KeyStat.Count), ListSortDirection.Descending));
        if (StatsView is ListCollectionView lcv)
        {
            lcv.IsLiveSorting = true;
            lcv.LiveSortingProperties.Add(nameof(KeyStat.Count));
        }
    }

    public override string Title => "Breakdown";
    public override string Subtitle => "Every key, counted and ranked.";
    public override string Glyph => "\uE8FD";
    public override int RefreshEveryTicks => 4; // ~1.25 Hz — the grid changes slowly

    public ICollectionView StatsView { get; }

    public TrackingService.Scope[] Scopes { get; } =
        { TrackingService.Scope.AllTime, TrackingService.Scope.Today, TrackingService.Scope.Session };

    private TrackingService.Scope _selectedScope = TrackingService.Scope.AllTime;
    public TrackingService.Scope SelectedScope
    {
        get => _selectedScope;
        set { if (SetProperty(ref _selectedScope, value)) Refresh(); }
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) StatsView.Refresh(); }
    }

    private long _totalCount;
    public long TotalCount { get => _totalCount; set => SetProperty(ref _totalCount, value); }

    private int _distinctCount;
    public int DistinctCount { get => _distinctCount; set => SetProperty(ref _distinctCount, value); }

    private bool FilterPredicate(object o)
    {
        if (string.IsNullOrWhiteSpace(_searchText)) return true;
        if (o is not KeyStat s) return false;
        return s.DisplayName.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || s.Category.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

    public override void Refresh()
    {
        var counts = Tracking.GetCounts(SelectedScope);

        long total = 0;
        long max = 0;
        foreach (var v in counts.Values) { total += v; if (v > max) max = v; }
        TotalCount = total;
        DistinctCount = counts.Count;

        foreach (var kv in counts)
        {
            double pct = total > 0 ? kv.Value * 100.0 / total : 0;
            double frac = max > 0 ? (double)kv.Value / max : 0;

            if (_byVk.TryGetValue(kv.Key, out var stat))
            {
                stat.Count = kv.Value;
                stat.Percentage = pct;
                stat.BarFraction = frac;
            }
            else
            {
                stat = new KeyStat(kv.Key, kv.Value) { Percentage = pct, BarFraction = frac };
                _byVk[kv.Key] = stat;
                _stats.Add(stat);
            }
        }

        // Zero-out keys that dropped out of the current scope.
        foreach (var stat in _byVk.Values)
        {
            if (!counts.ContainsKey(stat.VkCode) && stat.Count != 0)
            {
                stat.Count = 0;
                stat.Percentage = 0;
                stat.BarFraction = 0;
            }
        }
    }
}
