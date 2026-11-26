using PostAgent.Shared.Common;

namespace PostAgent.Chat.Agent;

public static class ChatMetadata
{
    // Written by the system from the Telegram update, never from the reader's text. It gives the model "now" in the reader's time zone,
    // so "yesterday" or "this morning" can become a period for search_posts.
    public static string Of(DateTimeOffset sentAt, TimeZoneInfo zone, string zoneId)
    {
        var local = TimeZoneInfo.ConvertTime(sentAt, zone);
        return $"Message metadata (set by the system, not written by the reader): the next message was sent {LocalTime.Stamp(sentAt, zone)}, "
            + $"{local.DayOfWeek}, time zone {zoneId} ({LocalTime.Offset(sentAt, zone)}). "
            + "Times in the digest and in tool results, and the dates and times you pass to search_posts, are in this time zone.";
    }
}
