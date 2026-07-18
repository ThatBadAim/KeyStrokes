using System.Windows.Media;
using KeyStrokes.Helpers;
using KeyStrokes.Services;

namespace KeyStrokes.Models;

/// <summary>A single row in the granular breakdown grid. Updated in place so the
/// grid keeps its sort order, selection, and scroll position across refreshes.</summary>
public sealed class KeyStat : ObservableObject
{
    public int VkCode { get; }
    public string DisplayName { get; }
    public string Category { get; }

    private long _count;
    public long Count { get => _count; set => SetProperty(ref _count, value); }

    private double _percentage;
    public double Percentage { get => _percentage; set => SetProperty(ref _percentage, value); }

    private double _barFraction;
    public double BarFraction { get => _barFraction; set => SetProperty(ref _barFraction, value); }

    public KeyStat(int vk, long count)
    {
        VkCode = vk;
        DisplayName = KeyMapper.FriendlyName(vk);
        Category = KeyMapper.Category(vk).ToString();
        _count = count;
    }
}

/// <summary>One keycap on the heatmap keyboard.</summary>
public sealed class HeatKey : ObservableObject
{
    public int VkCode { get; }
    public string Face { get; }
    public double WidthUnits { get; }
    public bool IsSpacer { get; }

    private long _count;
    public long Count { get => _count; set => SetProperty(ref _count, value); }

    private double _intensity;
    public double Intensity { get => _intensity; set => SetProperty(ref _intensity, value); }

    private Brush _fill = Brushes.Transparent;
    public Brush Fill { get => _fill; set => SetProperty(ref _fill, value); }

    private Brush _glow = Brushes.Transparent;
    public Brush Glow { get => _glow; set => SetProperty(ref _glow, value); }

    public HeatKey(KeyDef def)
    {
        VkCode = def.Vk;
        Face = def.Face;
        WidthUnits = def.Width;
        IsSpacer = def.IsSpacer;
    }
}

public sealed class HeatRow
{
    public IReadOnlyList<HeatKey> Keys { get; }
    public HeatRow(IReadOnlyList<HeatKey> keys) => Keys = keys;
}

/// <summary>One bar in the historical-trend chart.</summary>
public sealed class TrendBar : ObservableObject
{
    public string Label { get; }
    public string SubLabel { get; }
    public long Count { get; }

    private double _fraction;
    public double Fraction { get => _fraction; set => SetProperty(ref _fraction, value); }

    private bool _isPeak;
    public bool IsPeak { get => _isPeak; set => SetProperty(ref _isPeak, value); }

    public TrendBar(string label, string subLabel, long count)
    {
        Label = label;
        SubLabel = subLabel;
        Count = count;
    }
}
