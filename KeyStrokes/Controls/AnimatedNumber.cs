using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace KeyStrokes.Controls;

/// <summary>
/// A TextBlock whose displayed number eases toward its target value, giving the
/// live counters that crisp low-friction "odometer" motion on every update.
/// </summary>
public sealed class AnimatedNumber : TextBlock
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(AnimatedNumber),
            new PropertyMetadata(0.0, OnValueChanged));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty NumberFormatProperty =
        DependencyProperty.Register(nameof(NumberFormat), typeof(string), typeof(AnimatedNumber),
            new PropertyMetadata("N0"));

    public string NumberFormat
    {
        get => (string)GetValue(NumberFormatProperty);
        set => SetValue(NumberFormatProperty, value);
    }

    private static readonly DependencyProperty DisplayProperty =
        DependencyProperty.Register(nameof(Display), typeof(double), typeof(AnimatedNumber),
            new PropertyMetadata(0.0, OnDisplayChanged));

    private double Display
    {
        get => (double)GetValue(DisplayProperty);
        set => SetValue(DisplayProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (AnimatedNumber)d;
        double from = self.Display;
        double to = (double)e.NewValue;

        var anim = new DoubleAnimation(from, to, new Duration(TimeSpan.FromMilliseconds(600)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        self.BeginAnimation(DisplayProperty, anim);
    }

    private static void OnDisplayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (AnimatedNumber)d;
        self.Text = ((double)e.NewValue).ToString(self.NumberFormat, CultureInfo.CurrentCulture);
    }
}
