using PostAgent.Shared.Chat;

namespace PostAgent.Shared.Feedback;

// The /digest reports a chat can still rate (RatedDigests), one blob per chat.
public interface IRatedDigestStore
{
    // Null when the chat has no such report (never kept, or pushed out by newer ones).
    Task<ReportSnapshot?> FindAsync(long chatId, string reportId, CancellationToken ct);

    // Retries a lost race on its own.
    Task SaveAsync(long chatId, ReportSnapshot report, CancellationToken ct);
}
