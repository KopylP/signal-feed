using System.Globalization;

namespace PostAgent.Alerts;

public static class QuietHours
{
    public static bool IsQuiet(string spec, string timeZoneId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(spec))
        {
            return false;
        }

        if (!TryParseRange(spec, out var range))
        {
            throw new FormatException(InvalidRangeMessage(spec));
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var local = TimeOnly.FromTimeSpan(TimeZoneInfo.ConvertTime(now, zone).TimeOfDay);

        // Інтервал може переходити через північ.
        return range.Start <= range.End
            ? local >= range.Start && local < range.End
            : local >= range.Start || local < range.End;
    }

    public static string? Validate(string spec, string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(spec))
        {
            return null;
        }

        if (!TryParseRange(spec, out _))
        {
            return InvalidRangeMessage(spec);
        }

        return TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _)
            ? null
            : $"AlertTimeZone '{timeZoneId}' was not found on this system.";
    }

    private static bool TryParseRange(string spec, out (TimeOnly Start, TimeOnly End) range)
    {
        var parts = spec.Split('-', 2, StringSplitOptions.TrimEntries);
        range = default;

        if (parts.Length != 2
            || !TimeOnly.TryParse(parts[0], CultureInfo.InvariantCulture, out var start)
            || !TimeOnly.TryParse(parts[1], CultureInfo.InvariantCulture, out var end))
        {
            return false;
        }

        range = (start, end);
        return true;
    }

    private static string InvalidRangeMessage(string spec) =>
        $"AlertQuietHours must look like 23:00-07:00, got '{spec}'.";
}
