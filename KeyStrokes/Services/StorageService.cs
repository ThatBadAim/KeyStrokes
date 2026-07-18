using System.IO;
using System.Text.Json;
using KeyStrokes.Models;

namespace KeyStrokes.Services;

/// <summary>
/// Local-only persistence. Reads/writes a single JSON document under
/// %APPDATA%\KeyStrokes. Writes are atomic (temp file + replace) with a rolling
/// .bak, so an interrupted write or a crash can never corrupt the live data.
/// There is no network path anywhere in this class.
/// </summary>
public sealed class StorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _dir;
    private readonly string _file;
    private readonly string _tmp;
    private readonly string _bak;
    private readonly SemaphoreSlim _ioGate = new(1, 1);

    public string DataDirectory => _dir;
    public string DataFilePath => _file;

    public StorageService()
    {
        _dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KeyStrokes");
        _file = Path.Combine(_dir, "data.json");
        _tmp = _file + ".tmp";
        _bak = _file + ".bak";
    }

    public AppData Load()
    {
        try
        {
            Directory.CreateDirectory(_dir);

            if (File.Exists(_file))
            {
                var json = File.ReadAllText(_file);
                var data = JsonSerializer.Deserialize<AppData>(json, JsonOptions);
                if (data != null) return Normalize(data);
            }

            // Primary missing/corrupt — try the backup.
            if (File.Exists(_bak))
            {
                var json = File.ReadAllText(_bak);
                var data = JsonSerializer.Deserialize<AppData>(json, JsonOptions);
                if (data != null) return Normalize(data);
            }
        }
        catch
        {
            // Fall through to a fresh document; never crash on load.
        }

        return new AppData();
    }

    private static AppData Normalize(AppData data)
    {
        data.LifetimeKeyCounts ??= new();
        data.DailyKeyCounts ??= new();
        data.DailyMouseDistance ??= new();
        data.DailyScrollDistance ??= new();
        data.Settings ??= new AppSettings();
        data.Settings.ExcludedProcesses ??= new();
        data.Settings.ExcludedTitleKeywords ??= new();
        return data;
    }

    public async Task SaveAsync(AppData data, CancellationToken ct = default)
    {
        await _ioGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_dir);
            var json = JsonSerializer.Serialize(data, JsonOptions);

            await File.WriteAllTextAsync(_tmp, json, ct).ConfigureAwait(false);

            // Roll the current file into .bak, then atomically swap in the temp.
            if (File.Exists(_file))
            {
                File.Copy(_file, _bak, overwrite: true);
            }
            File.Move(_tmp, _file, overwrite: true);
        }
        finally
        {
            _ioGate.Release();
        }
    }
}
