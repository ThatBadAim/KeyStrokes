using System.Collections.ObjectModel;
using KeyStrokes.Models;
using KeyStrokes.Services;

namespace KeyStrokes.ViewModels;

public sealed class DashboardViewModel : PageViewModel
{
    public DashboardViewModel(TrackingService tracking) : base(tracking) { }

    public override string Title => "Dashboard";
    public override string Subtitle => "Your typing, live.";
    public override string Glyph => "\uE80F";

    private long _lifetimeTotal;
    public long LifetimeTotal { get => _lifetimeTotal; set => SetProperty(ref _lifetimeTotal, value); }

    private long _sessionTotal;
    public long SessionTotal { get => _sessionTotal; set => SetProperty(ref _sessionTotal, value); }

    private long _keysToday;
    public long KeysToday { get => _keysToday; set => SetProperty(ref _keysToday, value); }

    private int _kpm;
    public int Kpm { get => _kpm; set => SetProperty(ref _kpm, value); }

    private int _peakKpm;
    public int PeakKpm { get => _peakKpm; set => SetProperty(ref _peakKpm, value); }

    private double _kpmMax = 300;
    public double KpmMax { get => _kpmMax; set => SetProperty(ref _kpmMax, value); }

    private string _sessionDuration = "0m";
    public string SessionDuration { get => _sessionDuration; set => SetProperty(ref _sessionDuration, value); }

    private string _wpm = "0";
    public string Wpm { get => _wpm; set => SetProperty(ref _wpm, value); }

    private string _favoriteKey = "—";
    public string FavoriteKey { get => _favoriteKey; set => SetProperty(ref _favoriteKey, value); }

    private int _distinctKeys;
    public int DistinctKeys { get => _distinctKeys; set => SetProperty(ref _distinctKeys, value); }

    private long _sessionMouseClicks;
    public long SessionMouseClicks { get => _sessionMouseClicks; set => SetProperty(ref _sessionMouseClicks, value); }

    private double _sessionMouseDistance;
    public double SessionMouseDistance { get => _sessionMouseDistance; set => SetProperty(ref _sessionMouseDistance, value); }

    private double _sessionScrollDistance;
    public double SessionScrollDistance { get => _sessionScrollDistance; set => SetProperty(ref _sessionScrollDistance, value); }

    public ObservableCollection<KeyStat> TopKeys { get; } = new();

    public override void Refresh()
    {
        LifetimeTotal = Tracking.LifetimeTotal;
        SessionTotal = Tracking.SessionTotal;
        KeysToday = Tracking.KeysToday;

        int kpm = Tracking.GetCurrentKpm();
        Kpm = kpm;
        PeakKpm = Tracking.PeakKpm;
        Wpm = (kpm / 5.0).ToString("0"); // conventional 5 keystrokes ≈ 1 word

        double target = Math.Max(300, Math.Ceiling(Math.Max(kpm, PeakKpm) / 100.0) * 100);
        if (target > KpmMax || target < KpmMax - 100) KpmMax = target;

        var span = DateTime.UtcNow - Tracking.SessionStartUtc;
        SessionDuration = span.TotalHours >= 1
            ? $"{(int)span.TotalHours}h {span.Minutes}m"
            : $"{span.Minutes}m {span.Seconds}s";

        var sessionCounts = Tracking.GetCounts(TrackingService.Scope.Session);
        long clicks = 0;
        if (sessionCounts.TryGetValue(0x01, out var lc)) clicks += lc;
        if (sessionCounts.TryGetValue(0x02, out var rc)) clicks += rc;
        if (sessionCounts.TryGetValue(0x04, out var mc)) clicks += mc;
        if (sessionCounts.TryGetValue(0x05, out var xc1)) clicks += xc1;
        if (sessionCounts.TryGetValue(0x06, out var xc2)) clicks += xc2;
        SessionMouseClicks = clicks;

        SessionMouseDistance = Tracking.SessionMouseDistance;
        SessionScrollDistance = Tracking.SessionScrollDistance;

        RefreshTopKeys();
    }

    private void RefreshTopKeys()
    {
        var counts = Tracking.GetCounts(TrackingService.Scope.AllTime);
        DistinctKeys = counts.Count;

        long total = 0;
        foreach (var v in counts.Values) total += v;

        var top = counts.OrderByDescending(kv => kv.Value).Take(10).ToList();
        FavoriteKey = top.Count > 0 ? KeyMapper.FriendlyName(top[0].Key) : "—";

        // Rebuild the small (max 5) list in place.
        for (int i = 0; i < top.Count; i++)
        {
            long count = top[i].Value;
            double pct = total > 0 ? count * 100.0 / total : 0;
            double frac = top[0].Value > 0 ? (double)count / top[0].Value : 0;

            if (i < TopKeys.Count && TopKeys[i].VkCode == top[i].Key)
            {
                TopKeys[i].Count = count;
                TopKeys[i].Percentage = pct;
                TopKeys[i].BarFraction = frac;
            }
            else
            {
                var stat = new KeyStat(top[i].Key, count) { Percentage = pct, BarFraction = frac };
                if (i < TopKeys.Count) TopKeys[i] = stat;
                else TopKeys.Add(stat);
            }
        }
        while (TopKeys.Count > top.Count) TopKeys.RemoveAt(TopKeys.Count - 1);
    }
}
