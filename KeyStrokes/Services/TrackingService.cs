using System.Collections.Concurrent;
using System.Threading;
using KeyStrokes.Interop;
using KeyStrokes.Models;

namespace KeyStrokes.Services;

/// <summary>
/// The core engine. Owns the input monitor, the privacy gate, and all counters,
/// and drives persistence. The keystroke hot path is lock-free: presses are
/// folded into <see cref="ConcurrentDictionary{TKey,TValue}"/> counters and
/// interlocked totals on the monitor thread, while the UI simply polls the
/// current values a few times a second. That decoupling is what keeps input
/// latency at zero even under heavy typing or gaming.
/// </summary>
public sealed class TrackingService : IDisposable
{
    private const int KpmWindowMs = 60_000;
    private const int AutosaveIntervalMs = 15_000;

    private readonly Win32InputMonitor _monitor = new();
    private readonly PrivacyService _privacy = new();
    private readonly StorageService _storage = new();

    private readonly ConcurrentDictionary<int, long> _lifetime = new();
    private readonly ConcurrentDictionary<int, long> _session = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, long>> _daily = new();

    private readonly object _kpmLock = new();
    private readonly Queue<long> _kpmTimestamps = new();

    private long _lifetimeTotal;
    private long _sessionTotal;
    private int _peakKpm;

    private AppSettings _settings = new();
    private DateTime _installedUtc = DateTime.UtcNow;
    private DateTime _sessionStartUtc = DateTime.UtcNow;

    private volatile bool _enabled;
    private volatile bool _dirty;
    private Timer? _autosaveTimer;
    private bool _initialized;

    /// <summary>Raised (possibly off the UI thread) when capture/privacy state changes.</summary>
    public event Action? StateChanged;

    // ---- Public state (read by view models) ------------------------------
    public PrivacyService Privacy => _privacy;
    public StorageService Storage => _storage;
    public AppSettings Settings => _settings;

    public bool IsTrackingEnabled => _enabled;
    public bool IsExcluded => _privacy.IsCurrentlyExcluded;
    public string ExclusionReason => _privacy.CurrentReason;
    public bool IsActivelyCapturing => _enabled && !_privacy.IsCurrentlyExcluded;

    public long LifetimeTotal => Interlocked.Read(ref _lifetimeTotal);
    public long SessionTotal => Interlocked.Read(ref _sessionTotal);
    public int PeakKpm => _peakKpm;
    public DateTime SessionStartUtc => _sessionStartUtc;
    public DateTime InstalledUtc => _installedUtc;

    public long KeysToday
    {
        get
        {
            if (_daily.TryGetValue(TodayKey, out var day))
            {
                long sum = 0;
                foreach (var v in day.Values) sum += v;
                return sum;
            }
            return 0;
        }
    }

    private static string TodayKey => DateTime.Now.ToString("yyyy-MM-dd");

    // ---- Lifecycle -------------------------------------------------------
    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        var data = _storage.Load();
        _installedUtc = data.InstalledUtc;
        _settings = data.Settings;

        foreach (var kv in data.LifetimeKeyCounts)
        {
            _lifetime[kv.Key] = kv.Value;
            Interlocked.Add(ref _lifetimeTotal, kv.Value);
        }
        foreach (var day in data.DailyKeyCounts)
        {
            var bucket = new ConcurrentDictionary<int, long>();
            foreach (var kv in day.Value) bucket[kv.Key] = kv.Value;
            _daily[day.Key] = bucket;
        }

        _privacy.UpdateRules(_settings);
        _privacy.ExclusionChanged += () => StateChanged?.Invoke();

        _monitor.KeyDown += OnKeyDown;
        _monitor.ForegroundChanged += info => _privacy.Evaluate(info);

        _autosaveTimer = new Timer(_ => AutosaveTick(), null, AutosaveIntervalMs, AutosaveIntervalMs);

        SetTrackingEnabled(_settings.TrackingEnabled, persist: false);
    }

    public void SetTrackingEnabled(bool enabled, bool persist = true)
    {
        if (enabled)
        {
            _monitor.CaptureEnabled = true;
            _monitor.Start();          // installs WH_KEYBOARD_LL
        }
        else
        {
            _monitor.CaptureEnabled = false;
            _monitor.Stop();           // fully unhooks — zero CPU, absolute privacy
        }

        _enabled = enabled;
        _settings.TrackingEnabled = enabled;

        if (persist)
        {
            _dirty = true;
            _ = SaveNowAsync();
        }
        StateChanged?.Invoke();
    }

    // ---- Hot path (monitor thread) ---------------------------------------
    private void OnKeyDown(int vk)
    {
        // Second guard: even though the monitor is live, drop the press if the
        // focused app is on the privacy exclusion list.
        if (_privacy.IsCurrentlyExcluded) return;

        _lifetime.AddOrUpdate(vk, 1, static (_, v) => v + 1);
        _session.AddOrUpdate(vk, 1, static (_, v) => v + 1);

        var bucket = _daily.GetOrAdd(TodayKey, static _ => new ConcurrentDictionary<int, long>());
        bucket.AddOrUpdate(vk, 1, static (_, v) => v + 1);

        Interlocked.Increment(ref _lifetimeTotal);
        Interlocked.Increment(ref _sessionTotal);

        lock (_kpmLock)
        {
            _kpmTimestamps.Enqueue(Environment.TickCount64);
        }

        _dirty = true;
    }

    // ---- KPM -------------------------------------------------------------
    public int GetCurrentKpm()
    {
        long cutoff = Environment.TickCount64 - KpmWindowMs;
        int count;
        lock (_kpmLock)
        {
            while (_kpmTimestamps.Count > 0 && _kpmTimestamps.Peek() < cutoff)
                _kpmTimestamps.Dequeue();
            count = _kpmTimestamps.Count;
        }
        if (count > _peakKpm) _peakKpm = count;
        return count;
    }

    // ---- Snapshots for the views -----------------------------------------
    public enum Scope { AllTime, Today, Session }

    public IReadOnlyDictionary<int, long> GetCounts(Scope scope) => scope switch
    {
        Scope.Session => new Dictionary<int, long>(_session),
        Scope.Today => _daily.TryGetValue(TodayKey, out var d)
            ? new Dictionary<int, long>(d)
            : new Dictionary<int, long>(),
        _ => new Dictionary<int, long>(_lifetime),
    };

    /// <summary>Ordered (oldest→newest) per-day totals for the history view.</summary>
    public IReadOnlyList<(DateTime Date, long Total)> GetDailyTotals()
    {
        var list = new List<(DateTime, long)>();
        foreach (var kv in _daily)
        {
            if (DateTime.TryParse(kv.Key, out var date))
            {
                long sum = 0;
                foreach (var v in kv.Value.Values) sum += v;
                list.Add((date.Date, sum));
            }
        }
        list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return list;
    }

    // ---- Session / data management ---------------------------------------
    public void ClearSession()
    {
        _session.Clear();
        Interlocked.Exchange(ref _sessionTotal, 0);
        _peakKpm = 0;
        _sessionStartUtc = DateTime.UtcNow;
        lock (_kpmLock) _kpmTimestamps.Clear();
        StateChanged?.Invoke();
    }

    public async Task ClearAllDataAsync()
    {
        _lifetime.Clear();
        _session.Clear();
        _daily.Clear();
        Interlocked.Exchange(ref _lifetimeTotal, 0);
        Interlocked.Exchange(ref _sessionTotal, 0);
        _peakKpm = 0;
        _sessionStartUtc = DateTime.UtcNow;
        _installedUtc = DateTime.UtcNow;
        lock (_kpmLock) _kpmTimestamps.Clear();
        _dirty = true;
        await SaveNowAsync();
        StateChanged?.Invoke();
    }

    public void UpdatePrivacyRules(AppSettings settings)
    {
        _settings.ExcludedProcesses = settings.ExcludedProcesses;
        _settings.ExcludedTitleKeywords = settings.ExcludedTitleKeywords;
        _privacy.UpdateRules(_settings);
        _dirty = true;
    }

    /// <summary>A consistent copy of all data, for export.</summary>
    public AppData GetDataSnapshot() => BuildSnapshot();

    // ---- Persistence -----------------------------------------------------
    private AppData BuildSnapshot()
    {
        var data = new AppData
        {
            InstalledUtc = _installedUtc,
            Settings = _settings,
            LifetimeKeyCounts = new Dictionary<int, long>(_lifetime),
        };
        foreach (var day in _daily)
            data.DailyKeyCounts[day.Key] = new Dictionary<int, long>(day.Value);
        return data;
    }

    private void AutosaveTick()
    {
        if (!_dirty) return;
        _ = SaveNowAsync();
    }

    public async Task SaveNowAsync()
    {
        _dirty = false;
        try
        {
            await _storage.SaveAsync(BuildSnapshot());
        }
        catch
        {
            _dirty = true; // retry on next tick if the write failed
        }
    }

    public async Task ShutdownAsync()
    {
        _autosaveTimer?.Dispose();
        _autosaveTimer = null;
        _monitor.CaptureEnabled = false;
        _monitor.Stop();
        await SaveNowAsync();  // graceful commit on exit
    }

    public void Dispose()
    {
        _autosaveTimer?.Dispose();
        _monitor.Dispose();
    }
}
