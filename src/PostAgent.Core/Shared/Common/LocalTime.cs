using System.Globalization;

namespace PostAgent.Shared.Common;

// The reader's time zone is the AlertTimeZone setting. Storage and partitions stay in UTC; only what a person or the model reads is converted.
public static class LocalTime
{
    // An unknown id falls back to UTC instead of failing a chat turn; the settings validator already rejects it on startup.
    public static TimeZoneInfo Zone(string? id) =>
        id is not null && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) ? zone : TimeZoneInfo.Utc;

    public static string Stamp(DateTimeOffset moment, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(moment, zone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    public static string Day(DateTimeOffset moment, TimeZoneInfo zone, string format = "yyyy-MM-dd") =>
        TimeZoneInfo.ConvertTime(moment, zone).ToString(format, CultureInfo.InvariantCulture);

    // "UTC+02:00", "UTC-05:30" or "UTC".
    public static string Offset(DateTimeOffset moment, TimeZoneInfo zone)
    {
        var offset = zone.GetUtcOffset(moment);
        return offset == TimeSpan.Zero
            ? "UTC"
            : $"UTC{(offset < TimeSpan.Zero ? '-' : '+')}{offset.Duration().ToString(@"hh\:mm", CultureInfo.InvariantCulture)}";
    }

    public static string StampWithOffset(DateTimeOffset moment, TimeZoneInfo zone) =>
        $"{Stamp(moment, zone)} ({Offset(moment, zone)})";
}
