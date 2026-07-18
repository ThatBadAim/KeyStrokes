using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using KeyStrokes.Models;

namespace KeyStrokes.Services;

/// <summary>Writes the user's aggregate data out to formatted CSV or JSON files.</summary>
public static class ExportService
{
    public static async Task ExportKeyBreakdownCsvAsync(string path, IEnumerable<KeyStat> stats)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Rank,Key,Category,Count,Percentage");

        int rank = 1;
        foreach (var s in stats.OrderByDescending(s => s.Count))
        {
            sb.Append(rank++).Append(',')
              .Append(Csv(s.DisplayName)).Append(',')
              .Append(Csv(s.Category)).Append(',')
              .Append(s.Count).Append(',')
              .Append(s.Percentage.ToString("0.00", CultureInfo.InvariantCulture))
              .AppendLine();
        }

        await File.WriteAllTextAsync(path, sb.ToString(), new UTF8Encoding(true));
    }

    public static async Task ExportFullJsonAsync(string path, AppData data)
    {
        // Re-project into a friendly, self-describing shape rather than dumping
        // the raw storage model with numeric key codes.
        var export = new
        {
            exportedUtc = DateTime.UtcNow,
            installedUtc = data.InstalledUtc,
            lifetimeTotal = data.LifetimeTotal,
            lifetimeByKey = data.LifetimeKeyCounts
                .OrderByDescending(kv => kv.Value)
                .Select(kv => new
                {
                    key = KeyMapper.FriendlyName(kv.Key),
                    vkCode = kv.Key,
                    category = KeyMapper.Category(kv.Key).ToString(),
                    count = kv.Value,
                }),
            daily = data.DailyKeyCounts
                .OrderBy(kv => kv.Key)
                .Select(kv => new
                {
                    date = kv.Key,
                    total = kv.Value.Values.Sum(),
                    byKey = kv.Value
                        .OrderByDescending(k => k.Value)
                        .Select(k => new { key = KeyMapper.FriendlyName(k.Key), count = k.Value }),
                }),
        };

        var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, new UTF8Encoding(true));
    }

    private static string Csv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
