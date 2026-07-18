using System.Collections.ObjectModel;
using System.Globalization;
using KeyStrokes.Models;
using KeyStrokes.Services;

namespace KeyStrokes.ViewModels;

public sealed class HistoryViewModel : PageViewModel
{
    public HistoryViewModel(TrackingService tracking) : base(tracking) { }

    public override string Title => "History";
    public override string Subtitle => "Trends over time.";
    public override string Glyph => "\uE81C";
    public override int RefreshEveryTicks => 150; // ~30s — trend bars rebuild rarely to avoid re-animation churn

    public enum Grouping { Day, Week, Month }

    public Grouping[] Groupings { get; } = { Grouping.Day, Grouping.Week, Grouping.Month };

    private Grouping _selectedGrouping = Grouping.Day;
    public Grouping SelectedGrouping
    {
        get => _selectedGrouping;
        set { if (SetProperty(ref _selectedGrouping, value)) Refresh(); }
    }

    public ObservableCollection<TrendBar> Bars { get; } = new();

    private long _rangeTotal;
    public long RangeTotal { get => _rangeTotal; set => SetProperty(ref _rangeTotal, value); }

    private long _busiestCount;
    public long BusiestCount { get => _busiestCount; set => SetProperty(ref _busiestCount, value); }

    private string _busiestLabel = "—";
    public string BusiestLabel { get => _busiestLabel; set => SetProperty(ref _busiestLabel, value); }

    private long _dailyAverage;
    public long DailyAverage { get => _dailyAverage; set => SetProperty(ref _dailyAverage, value); }

    private int _activeDays;
    public int ActiveDays { get => _activeDays; set => SetProperty(ref _activeDays, value); }

    public override void Refresh()
    {
        var daily = Tracking.GetDailyTotals();
        Bars.Clear();

        if (daily.Count == 0)
        {
            RangeTotal = 0; BusiestCount = 0; BusiestLabel = "—"; DailyAverage = 0; ActiveDays = 0;
            return;
        }

        ActiveDays = daily.Count;
        long grand = daily.Sum(d => d.Total);
        RangeTotal = grand;
        DailyAverage = grand / Math.Max(1, daily.Count);

        var buckets = _selectedGrouping switch
        {
            Grouping.Week => GroupWeeks(daily),
            Grouping.Month => GroupMonths(daily),
            _ => GroupDays(daily),
        };

        long max = buckets.Count > 0 ? buckets.Max(b => b.Count) : 0;
        var busiest = buckets.OrderByDescending(b => b.Count).FirstOrDefault();
        BusiestCount = busiest.Count;
        BusiestLabel = busiest.Full ?? "—";

        foreach (var b in buckets)
        {
            var bar = new TrendBar(b.Label, b.Sub, b.Count)
            {
                Fraction = max > 0 ? (double)b.Count / max : 0,
                IsPeak = b.Count == max && max > 0,
            };
            Bars.Add(bar);
        }
    }

    private readonly record struct Bucket(string Label, string Sub, string Full, long Count);

    private static List<Bucket> GroupDays(IReadOnlyList<(DateTime Date, long Total)> daily)
    {
        // Last 21 days for readability.
        return daily.TakeLast(21).Select(d => new Bucket(
            d.Date.Day.ToString(CultureInfo.CurrentCulture),
            d.Date.ToString("ddd", CultureInfo.CurrentCulture),
            d.Date.ToString("dddd, MMM d", CultureInfo.CurrentCulture),
            d.Total)).ToList();
    }

    private static List<Bucket> GroupWeeks(IReadOnlyList<(DateTime Date, long Total)> daily)
    {
        var cal = CultureInfo.CurrentCulture.Calendar;
        var groups = daily
            .GroupBy(d => StartOfWeek(d.Date))
            .OrderBy(g => g.Key)
            .TakeLast(16)
            .Select(g => new Bucket(
                g.Key.ToString("MMM d", CultureInfo.CurrentCulture),
                "wk",
                $"Week of {g.Key:MMM d, yyyy}",
                g.Sum(x => x.Total)))
            .ToList();
        return groups;
    }

    private static List<Bucket> GroupMonths(IReadOnlyList<(DateTime Date, long Total)> daily)
    {
        return daily
            .GroupBy(d => new DateTime(d.Date.Year, d.Date.Month, 1))
            .OrderBy(g => g.Key)
            .TakeLast(12)
            .Select(g => new Bucket(
                g.Key.ToString("MMM", CultureInfo.CurrentCulture),
                g.Key.ToString("yyyy", CultureInfo.CurrentCulture),
                g.Key.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
                g.Sum(x => x.Total)))
            .ToList();
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }
}
