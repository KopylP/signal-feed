using System.Globalization;

namespace PostAgent.Chat.Agent.Tools;

// The period a post search covers, in UTC. Posts older than the retention are deleted, so the window never starts before it.
public readonly record struct PostSearchWindow(DateTimeOffset Since, DateTimeOffset Until)
{
    public static bool TryCreate(
        string? from, string? to, int days, DateTimeOffset now, int retentionDays, out PostSearchWindow window, out string error)
    {
        window = default;
        error = string.Empty;

        if (!TryParse(from, endOfDay: false, out var start))
        {
            error = "Could not read 'from'. Use ISO 8601 in UTC, for example 2026-11-25 or 2026-11-25T09:30.";
            return false;
        }

        if (!TryParse(to, endOfDay: true, out var end))
        {
            error = "Could not read 'to'. Use ISO 8601 in UTC, for example 2026-11-25 or 2026-11-25T18:00.";
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

        window = new PostSearchWindow(since, until);
        return true;
    }

    public override string ToString() =>
        $"{Since.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} to {Until.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC";

    // A date without a time means the whole day: the end is the start of the next day.
    private static bool TryParse(string? text, bool endOfDay, out DateTimeOffset? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var trimmed = text.Trim();
        if (!DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return false;
        }

        var dateOnly = DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        value = endOfDay && dateOnly ? parsed.AddDays(1) : parsed;
        return true;
    }
}
