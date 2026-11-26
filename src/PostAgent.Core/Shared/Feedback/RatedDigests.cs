using System.Text.Json;
using PostAgent.Shared.Chat;

namespace PostAgent.Shared.Feedback;

// The /digest reports of one chat whose 👍/👎 can still be pressed. The conversation keeps only the daily digest, so a /digest
// keeps its own entries here; the latest MaxKept are enough, an older button answers "outdated".
public static class RatedDigests
{
    public const int MaxKept = 5;

    // A vote needs the post, the channel and the title: the excerpt is dropped, so the blob stays small.
    public static IReadOnlyList<ReportSnapshot> Add(IEnumerable<ReportSnapshot> current, ReportSnapshot report) =>
        current
            .Where(r => r.ReportId != report.ReportId)
            .Append(report with { Entries = report.Entries.Select(e => e with { Excerpt = string.Empty }).ToList() })
            .TakeLast(MaxKept)
            .ToList();

    // Never throws: a damaged file reads as empty, and the next /digest replaces it.
    public static IReadOnlyList<ReportSnapshot> Parse(string json)
    {
        try
        {
            return (JsonSerializer.Deserialize<List<ReportSnapshot?>>(json, FeedbackJson.Options) ?? [])
                .OfType<ReportSnapshot>()
                .Where(r => r.ReportId is { Length: > 0 } && r.Entries is not null)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string Write(IReadOnlyList<ReportSnapshot> reports) => JsonSerializer.Serialize(reports, FeedbackJson.Options);
}
