using System.Globalization;

namespace PostAgent.Shared.Stats;

// The usage blob is one list of day-and-purpose rows. Only the latest KeepDays days are kept, so it stays small.
public static class UsageLog
{
    public const int KeepDays = 30;

    public static IReadOnlyList<UsageDay> Add(IEnumerable<UsageDay> current, DateOnly day, string purpose, long input, long output)
    {
        var key = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var rows = current.ToList();
        var existing = rows.FirstOrDefault(r => r.Day == key && r.Purpose == purpose);
        if (existing is not null)
        {
            rows.Remove(existing);
        }

        rows.Add(existing is null
            ? new UsageDay(key, purpose, 1, input, output)
            : existing with { Calls = existing.Calls + 1, InputTokens = existing.InputTokens + input, OutputTokens = existing.OutputTokens + output });

        var oldest = day.AddDays(1 - KeepDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return rows.Where(r => string.CompareOrdinal(r.Day, oldest) >= 0).OrderBy(r => r.Day, StringComparer.Ordinal).ThenBy(r => r.Purpose, StringComparer.Ordinal).ToList();
    }
}
