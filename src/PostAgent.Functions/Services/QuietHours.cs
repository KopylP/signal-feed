using System.Globalization;

namespace PostAgent.Services;

/// <summary>Тихі години алертів, наприклад "23:00-07:00" у часовому поясі AlertTimeZone.</summary>
public static class QuietHours
{
    public static bool IsQuiet(string spec, string timeZoneId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(spec))
        {
            return false;
        }

        var parts = spec.Split('-', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !TimeOnly.TryParse(parts[0], CultureInfo.InvariantCulture, out var start)
            || !TimeOnly.TryParse(parts[1], CultureInfo.InvariantCulture, out var end))
        {
            throw new FormatException($"AlertQuietHours must look like 23:00-07:00, got '{spec}'.");
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var local = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);

        // Інтервал може переходити через північ.
        return start <= end ? local >= start && local < end : local >= start || local < end;
    }
}
