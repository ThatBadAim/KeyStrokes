using System.Text.Json.Serialization;

namespace KeyStrokes.Models;

/// <summary>
/// The complete on-disk state. Stored as a single JSON document. Note that this
/// only ever holds *aggregate counts* — per-key totals and per-day totals. The
/// order keys were pressed in is never recorded, so typed content (passwords,
/// messages, etc.) is not reconstructable from this file by design.
/// </summary>
public sealed class AppData
{
    public int SchemaVersion { get; set; } = 1;

    public DateTime InstalledUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Lifetime total presses per virtual-key code.</summary>
    public Dictionary<int, long> LifetimeKeyCounts { get; set; } = new();

    /// <summary>Per-day totals, keyed by "yyyy-MM-dd" then by virtual-key code.</summary>
    public Dictionary<string, Dictionary<int, long>> DailyKeyCounts { get; set; } = new();

    public double LifetimeMouseDistance { get; set; }
    public double LifetimeScrollDistance { get; set; }

    public Dictionary<string, double> DailyMouseDistance { get; set; } = new();
    public Dictionary<string, double> DailyScrollDistance { get; set; } = new();

    public AppSettings Settings { get; set; } = new();

    [JsonIgnore]
    public long LifetimeTotal
    {
        get
        {
            long sum = 0;
            foreach (var v in LifetimeKeyCounts.Values) sum += v;
            return sum;
        }
    }
}

public sealed class AppSettings
{
    /// <summary>Whether capture is armed. The user's master toggle persists here.</summary>
    public bool TrackingEnabled { get; set; } = true;

    /// <summary>Minimize to the system tray instead of exiting when the window closes.</summary>
    public bool MinimizeToTrayOnClose { get; set; } = true;

    /// <summary>Process names (without extension, case-insensitive) that pause capture.</summary>
    public List<string> ExcludedProcesses { get; set; } = new()
    {
        "keepass", "keepassxc", "1password", "1passwordexe", "bitwarden",
        "lastpass", "dashlane", "nordpass", "protonpass", "enpass", "keeper",
    };

    /// <summary>Foreground window-title keywords (case-insensitive) that pause capture.</summary>
    public List<string> ExcludedTitleKeywords { get; set; } = new()
    {
        "password", "sign in", "signin", "log in", "login", "authentic",
        "unlock", "credential", "passphrase",
    };
}
