using System.Globalization;
using System.Text.Json;

namespace PostAgent.Shared.Cleanup;

// "The summaries are written": the message DailySummaryJob queues when every finished day through SummarizedThrough has its daily summary.
// Only then does the cleanup delete, and never a day after that one.
public sealed record CleanupRequest(string SummarizedThrough)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static CleanupRequest For(DateOnly day) => new(day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    public DateOnly Day => DateOnly.ParseExact(SummarizedThrough, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public string ToMessage() => JsonSerializer.Serialize(this, Options);

    // Null for anything that is not a complete request: such a message can never succeed, so it must not be retried.
    public static CleanupRequest? Parse(string message)
    {
        try
        {
            var request = JsonSerializer.Deserialize<CleanupRequest>(message, Options);
            return request is { SummarizedThrough: not null }
                && DateOnly.TryParseExact(request.SummarizedThrough, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                ? request
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
