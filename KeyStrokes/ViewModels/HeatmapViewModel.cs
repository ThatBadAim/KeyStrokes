using System.Collections.ObjectModel;
using KeyStrokes.Helpers;
using KeyStrokes.Models;
using KeyStrokes.Services;

namespace KeyStrokes.ViewModels;

public sealed class HeatmapViewModel : PageViewModel
{
    private readonly Dictionary<int, HeatKey> _byVk = new();

    private long _leftClickCount;
    public long LeftClickCount { get => _leftClickCount; set => SetProperty(ref _leftClickCount, value); }

    private long _rightClickCount;
    public long RightClickCount { get => _rightClickCount; set => SetProperty(ref _rightClickCount, value); }

    private long _middleClickCount;
    public long MiddleClickCount { get => _middleClickCount; set => SetProperty(ref _middleClickCount, value); }

    private long _x1ClickCount;
    public long X1ClickCount { get => _x1ClickCount; set => SetProperty(ref _x1ClickCount, value); }

    private long _x2ClickCount;
    public long X2ClickCount { get => _x2ClickCount; set => SetProperty(ref _x2ClickCount, value); }

    private long _scrollCount;
    public long ScrollCount { get => _scrollCount; set => SetProperty(ref _scrollCount, value); }

    private System.Windows.Media.Brush _leftClickFill = System.Windows.Media.Brushes.Transparent;
    public System.Windows.Media.Brush LeftClickFill { get => _leftClickFill; set => SetProperty(ref _leftClickFill, value); }

    private System.Windows.Media.Brush _rightClickFill = System.Windows.Media.Brushes.Transparent;
    public System.Windows.Media.Brush RightClickFill { get => _rightClickFill; set => SetProperty(ref _rightClickFill, value); }

    private System.Windows.Media.Brush _middleClickFill = System.Windows.Media.Brushes.Transparent;
    public System.Windows.Media.Brush MiddleClickFill { get => _middleClickFill; set => SetProperty(ref _middleClickFill, value); }

    private System.Windows.Media.Brush _x1ClickFill = System.Windows.Media.Brushes.Transparent;
    public System.Windows.Media.Brush X1ClickFill { get => _x1ClickFill; set => SetProperty(ref _x1ClickFill, value); }

    private System.Windows.Media.Brush _x2ClickFill = System.Windows.Media.Brushes.Transparent;
    public System.Windows.Media.Brush X2ClickFill { get => _x2ClickFill; set => SetProperty(ref _x2ClickFill, value); }

    private System.Windows.Media.Brush _scrollFill = System.Windows.Media.Brushes.Transparent;
    public System.Windows.Media.Brush ScrollFill { get => _scrollFill; set => SetProperty(ref _scrollFill, value); }

    private double _mouseDistance;
    public double MouseDistance { get => _mouseDistance; set => SetProperty(ref _mouseDistance, value); }

    private double _scrollDistance;
    public double ScrollDistance { get => _scrollDistance; set => SetProperty(ref _scrollDistance, value); }

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
            // Do not treat custom virtual/pseudo keys like scroll/movement as hottest key if unwanted, but mouse clicks (0x01, 0x02, etc.) can be counted.
            // Let's filter out pseudo keys >= 0x100 from being the 'hottest key' on the keyboard heatmap
            if (kv.Key < 0x100)
            {
                total += kv.Value;
                if (kv.Value > max) { max = kv.Value; hottestVk = kv.Key; }
            }
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

        // Mouse clicks and scroll heat highlights
        long leftCount = counts.TryGetValue(0x01, out var lc) ? lc : 0;
        long rightCount = counts.TryGetValue(0x02, out var rc) ? rc : 0;
        long middleCount = counts.TryGetValue(0x04, out var mc) ? mc : 0;
        long x1Count = counts.TryGetValue(0x05, out var xc1) ? xc1 : 0;
        long x2Count = counts.TryGetValue(0x06, out var xc2) ? xc2 : 0;
        long scrollCount = (counts.TryGetValue(0x101, out var s1) ? s1 : 0) + (counts.TryGetValue(0x102, out var s2) ? s2 : 0);

        LeftClickCount = leftCount;
        RightClickCount = rightCount;
        MiddleClickCount = middleCount;
        X1ClickCount = x1Count;
        X2ClickCount = x2Count;
        ScrollCount = scrollCount;

        LeftClickFill = ColorUtil.HeatBrush(max > 0 && leftCount > 0 ? Math.Pow((double)leftCount / max, 0.55) : 0);
        RightClickFill = ColorUtil.HeatBrush(max > 0 && rightCount > 0 ? Math.Pow((double)rightCount / max, 0.55) : 0);
        MiddleClickFill = ColorUtil.HeatBrush(max > 0 && middleCount > 0 ? Math.Pow((double)middleCount / max, 0.55) : 0);
        X1ClickFill = ColorUtil.HeatBrush(max > 0 && x1Count > 0 ? Math.Pow((double)x1Count / max, 0.55) : 0);
        X2ClickFill = ColorUtil.HeatBrush(max > 0 && x2Count > 0 ? Math.Pow((double)x2Count / max, 0.55) : 0);
        ScrollFill = ColorUtil.HeatBrush(max > 0 && scrollCount > 0 ? Math.Pow((double)scrollCount / max, 0.55) : 0);

        MouseDistance = Tracking.GetMouseDistance(SelectedScope);
        ScrollDistance = Tracking.GetScrollDistance(SelectedScope);
    }
}
