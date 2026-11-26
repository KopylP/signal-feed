using System.Globalization;
using PostAgent.Shared.Posts;

namespace PostAgent.Digest;

// What the reader wrote after /digest, as a period. Words and numbers are read in the reader's time zone; two ISO values
// (2026-11-25T18:00 2026-11-25T23:00, a date means the whole day) give an exact period. Never past now or before the retention.
public static class DigestPeriod
{
    public static readonly TimeSpan DefaultLength = TimeSpan.FromHours(24);

    // Null when the text is not understood or the period holds no stored posts.
    public static PostPeriod? Parse(string? arguments, DateTimeOffset now, TimeZoneInfo zone, int retentionDays)
    {
        var parts = (arguments ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var oldest = now.AddDays(-retentionDays);
        return parts switch
        {
            [] => Clamp(now - DefaultLength, now, oldest, now),
            [var one] when Hours(one) is { } hours => Clamp(now.AddHours(-hours), now, oldest, now),
            [var one] when IsWord(one, "today", "сьогодні") => Clamp(StartOfDay(now, zone), now, oldest, now),
            [var one] when IsWord(one, "yesterday", "вчора") => Clamp(StartOfDay(now, zone).AddDays(-1), StartOfDay(now, zone), oldest, now),
            [var from] => Exact(from, null, now, zone, retentionDays),
            [var from, var to] => Exact(from, to, now, zone, retentionDays),
            _ => null,
        };
    }

    private static PostPeriod? Exact(string from, string? to, DateTimeOffset now, TimeZoneInfo zone, int retentionDays) =>
        PostPeriod.TryCreate(from, to, 0, now, retentionDays, zone, out var period, out _) ? period : null;

    private static int? Hours(string text) =>
        int.TryParse(text.TrimEnd('h', 'H', 'г'), NumberStyles.None, CultureInfo.InvariantCulture, out var hours) && hours > 0 ? hours : null;

    private static bool IsWord(string text, params string[] words) => words.Contains(text, StringComparer.OrdinalIgnoreCase);

    private static DateTimeOffset StartOfDay(DateTimeOffset now, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(now, zone).Date;
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    private static PostPeriod? Clamp(DateTimeOffset since, DateTimeOffset until, DateTimeOffset oldest, DateTimeOffset now)
    {
        since = since < oldest ? oldest : since;
        until = until > now ? now : until;
        return since < until ? new PostPeriod(since, until) : null;
    }
}
