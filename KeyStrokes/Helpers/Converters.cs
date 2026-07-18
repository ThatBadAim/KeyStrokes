using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace KeyStrokes.Helpers;

/// <summary>Inverts a boolean.</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => value is bool b ? !b : value;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => value is bool b ? !b : value;
}

/// <summary>bool → Visibility (true = Visible). Pass "Invert" as parameter to flip.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        bool b = value is bool v && v;
        if (p as string == "Invert") b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Non-empty string → Visible, otherwise Collapsed.</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Heatmap key-unit width → device pixels. ConverterParameter overrides the base unit.</summary>
public sealed class KeyWidthConverter : IValueConverter
{
    public const double BaseUnit = 46.0;
    public const double Gap = 6.0;

    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        double units = value is double d ? d : 1.0;
        double baseUnit = BaseUnit;
        if (p is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var bu))
            baseUnit = bu;
        return units * baseUnit + (units - 1) * Gap;
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Fraction (0..1) → pixel width, scaled by the ConverterParameter (max width).</summary>
public sealed class FractionToWidthConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        double frac = value is double d ? d : 0;
        double max = 100;
        if (p is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var m))
            max = m;
        return Math.Max(0, frac) * max;
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Fraction (0..1) → pixel height for vertical trend bars (ConverterParameter = max height).</summary>
public sealed class FractionToHeightConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        double frac = value is double d ? d : 0;
        double max = 160;
        if (p is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var m))
            max = m;
        return Math.Max(2, frac * max);
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Long/number → grouped string with thousands separators.</summary>
public sealed class ThousandsConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => System.Convert.ToInt64(value).ToString("N0", CultureInfo.CurrentCulture);
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Maps the Scope / Grouping enum values to friendly dropdown labels.</summary>
public sealed class EnumLabelConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => value?.ToString() switch
    {
        "AllTime" => "All time",
        "Today" => "Today",
        "Session" => "This session",
        "Day" => "Daily",
        "Week" => "Weekly",
        "Month" => "Monthly",
        var s => s ?? string.Empty,
    };
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Only high-intensity keys get a colored glow, to keep the heatmap cheap to render.</summary>
public sealed class IntensityToGlowConverter : IValueConverter
{
    public object? Convert(object value, Type t, object p, CultureInfo c)
    {
        double intensity = value is double d ? d : 0;
        if (intensity < 0.45) return null;
        var color = ColorUtil.Heat(intensity);
        return new DropShadowEffect
        {
            Color = color,
            BlurRadius = 8 + intensity * 22,
            ShadowDepth = 0,
            Opacity = 0.35 + intensity * 0.45,
        };
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}
