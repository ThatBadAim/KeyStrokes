using System.Windows.Media;

namespace KeyStrokes.Helpers;

/// <summary>Heatmap color ramp: cold slate/blue → indigo → magenta → hot neon pink.</summary>
public static class ColorUtil
{
    private readonly record struct Stop(double Pos, byte R, byte G, byte B);

    private static readonly Stop[] Ramp =
    {
        new(0.00, 0x18, 0x1D, 0x2E), // near-background slate (unused keys)
        new(0.12, 0x1E, 0x3A, 0x8A), // deep blue
        new(0.32, 0x25, 0x63, 0xEB), // blue
        new(0.52, 0x63, 0x66, 0xF1), // indigo
        new(0.72, 0xA8, 0x38, 0xE0), // violet
        new(0.88, 0xE0, 0x2C, 0xB8), // magenta
        new(1.00, 0xFF, 0x3D, 0x9A), // hot neon pink
    };

    public static Color Heat(double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        for (int i = 1; i < Ramp.Length; i++)
        {
            if (t <= Ramp[i].Pos)
            {
                var a = Ramp[i - 1];
                var b = Ramp[i];
                double span = b.Pos - a.Pos;
                double f = span <= 0 ? 0 : (t - a.Pos) / span;
                return Color.FromRgb(
                    Lerp(a.R, b.R, f),
                    Lerp(a.G, b.G, f),
                    Lerp(a.B, b.B, f));
            }
        }
        var last = Ramp[^1];
        return Color.FromRgb(last.R, last.G, last.B);
    }

    private static byte Lerp(byte a, byte b, double f) => (byte)Math.Round(a + (b - a) * f);

    /// <summary>A frozen vertical gradient brush for a keycap at the given intensity.</summary>
    public static Brush HeatBrush(double intensity)
    {
        var top = Heat(intensity);
        // Slightly darker bottom for a subtle keycap curvature.
        var bottom = Color.FromRgb(
            (byte)(top.R * 0.82), (byte)(top.G * 0.82), (byte)(top.B * 0.82));

        var brush = new LinearGradientBrush(top, bottom, 90);
        brush.Freeze();
        return brush;
    }
}
