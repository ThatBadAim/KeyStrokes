using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace KeyStrokes.Controls;

/// <summary>
/// A radial "speedometer" that sweeps 270°. The needle eases toward the current
/// value so live KPM changes read as smooth motion rather than jumps.
/// </summary>
public sealed class KpmGauge : FrameworkElement
{
    private const double StartDeg = 135.0;
    private const double SweepDeg = 270.0;

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(KpmGauge),
            new FrameworkPropertyMetadata(0.0, OnValueChanged));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(KpmGauge),
            new FrameworkPropertyMetadata(300.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    private static readonly DependencyProperty RenderValueProperty =
        DependencyProperty.Register(nameof(RenderValue), typeof(double), typeof(KpmGauge),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private double RenderValue
    {
        get => (double)GetValue(RenderValueProperty);
        set => SetValue(RenderValueProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (KpmGauge)d;
        var anim = new DoubleAnimation((double)e.NewValue, new Duration(TimeSpan.FromMilliseconds(320)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        self.BeginAnimation(RenderValueProperty, anim);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        double cx = w / 2;
        double cy = h * 0.60;
        double radius = Math.Min(w / 2, h * 0.62) - 14;
        if (radius <= 0) return;

        double thickness = Math.Max(6, radius * 0.16);
        double max = Maximum <= 0 ? 1 : Maximum;
        double frac = Math.Clamp(RenderValue / max, 0, 1);

        // Track
        var trackPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), thickness)
        { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawGeometry(null, trackPen, BuildArc(cx, cy, radius, 0, 1));

        // Value arc with an indigo→pink gradient
        var grad = new LinearGradientBrush(
            Color.FromRgb(0x63, 0x66, 0xF1), Color.FromRgb(0xFF, 0x3D, 0x9A), 0);
        grad.Freeze();
        var valuePen = new Pen(grad, thickness)
        { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (frac > 0.001)
            dc.DrawGeometry(null, valuePen, BuildArc(cx, cy, radius, 0, frac));

        // Minor tick marks
        var tickBrush = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255));
        for (int i = 0; i <= 10; i++)
        {
            double t = i / 10.0;
            double ang = (StartDeg + t * SweepDeg) * Math.PI / 180.0;
            double r1 = radius - thickness - 4;
            double r2 = r1 - (i % 5 == 0 ? 10 : 5);
            var p1 = new Point(cx + r1 * Math.Cos(ang), cy + r1 * Math.Sin(ang));
            var p2 = new Point(cx + r2 * Math.Cos(ang), cy + r2 * Math.Sin(ang));
            dc.DrawLine(new Pen(tickBrush, i % 5 == 0 ? 2.5 : 1.5), p1, p2);
        }

        // Needle
        double needleAng = (StartDeg + frac * SweepDeg) * Math.PI / 180.0;
        double needleLen = radius - thickness - 2;
        var tip = new Point(cx + needleLen * Math.Cos(needleAng), cy + needleLen * Math.Sin(needleAng));
        var backAng = needleAng + Math.PI;
        var tail = new Point(cx + 14 * Math.Cos(backAng), cy + 14 * Math.Sin(backAng));

        var needleColor = Helpers.ColorUtil.Heat(0.4 + frac * 0.6);
        var needlePen = new Pen(new SolidColorBrush(needleColor), 3.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawLine(needlePen, tail, tip);

        // Hub
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x0B, 0x0F, 0x19)), new Pen(new SolidColorBrush(needleColor), 3), new Point(cx, cy), 8, 8);
    }

    private static Geometry BuildArc(double cx, double cy, double r, double fracStart, double fracEnd)
    {
        double a0 = (StartDeg + fracStart * SweepDeg) * Math.PI / 180.0;
        double a1 = (StartDeg + fracEnd * SweepDeg) * Math.PI / 180.0;
        var start = new Point(cx + r * Math.Cos(a0), cy + r * Math.Sin(a0));
        var end = new Point(cx + r * Math.Cos(a1), cy + r * Math.Sin(a1));

        bool large = (fracEnd - fracStart) * SweepDeg > 180.0;

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(start, false, false);
            ctx.ArcTo(end, new Size(r, r), 0, large, SweepDirection.Clockwise, true, false);
        }
        geo.Freeze();
        return geo;
    }
}
