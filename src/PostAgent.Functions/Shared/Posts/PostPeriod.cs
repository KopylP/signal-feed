using System.Globalization;
using PostAgent.Shared.Common;

namespace PostAgent.Shared.Posts;

// The period of posts a search or an on-demand digest covers. A date or time without an offset is read in the reader's time zone.
// Posts older than the retention are deleted, so the window never starts before it.
public readonly record struct PostPeriod(DateTimeOffset Since, DateTimeOffset Until)
{
    public static bool TryCreate(
        string? from, string? to, int days, DateTimeOffset now, int retentionDays, TimeZoneInfo zone,
        out PostPeriod window, out string error)
    {
        window = default;
        error = string.Empty;

        if (!TryParse(from, endOfDay: false, zone, out var start))
        {
            error = "Could not read 'from'. Use ISO 8601 in the reader's time zone, for example 2026-11-25 or 2026-11-25T09:30.";
            return false;
        }

        if (!TryParse(to, endOfDay: true, zone, out var end))
        {
            error = "Could not read 'to'. Use ISO 8601 in the reader's time zone, for example 2026-11-25 or 2026-11-25T18:00.";
            return false;
        }

        var oldest = now.AddDays(-retentionDays);
        var since = start ?? now.AddDays(-Math.Clamp(days <= 0 ? 1 : days, 1, retentionDays));
        var until = end ?? now;

        since = since < oldest ? oldest : since;
        until = until > now ? now : until;

        if (since >= until)
        {
            error = $"Nothing is stored for that period: posts are kept for the last {retentionDays} day(s), until now.";
            return false;
        }

        window = new PostPeriod(since, until);
        return true;
    }

    public string Describe(TimeZoneInfo zone) =>
        $"{LocalTime.Stamp(Since, zone)} to {LocalTime.Stamp(Until, zone)} ({LocalTime.Offset(Until, zone)})";

    // A date without a time means the whole day: the end is the start of the next day.
    public static bool TryParse(string? text, bool endOfDay, TimeZoneInfo zone, out DateTimeOffset? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var trimmed = text.Trim();
        if (!DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            return false;
        }

        var dateOnly = DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        if (endOfDay && dateOnly)
        {
            parsed = parsed.AddDays(1);
        }

        // No offset in the text: it is the reader's wall clock. An explicit Z or offset is kept as written.
        value = parsed.Kind == DateTimeKind.Unspecified
            ? new DateTimeOffset(parsed, zone.GetUtcOffset(parsed))
            : new DateTimeOffset(parsed.ToUniversalTime(), TimeSpan.Zero);
        return true;
    }
}
