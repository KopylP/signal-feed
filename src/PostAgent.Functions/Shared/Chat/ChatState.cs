using System.Text.Json;

namespace PostAgent.Shared.Chat;

// The conversation about one digest: the snapshot, the last handled Telegram update ids and the serialized agent session.
// A set of ids, not a watermark: messages are handled in parallel, and Telegram may restart update ids.
public sealed record ChatState(ReportSnapshot Report, IReadOnlyList<long> ProcessedUpdateIds, JsonElement? Session)
{
    public const int MaxProcessedIds = 20;

    // The reader asked to forget the conversation: the digest stays, so the next question starts a fresh thread about it.
    public ChatState WithoutSession() => this with { Session = null };

    public ChatState WithProcessed(long updateId, JsonElement session) =>
        this with
        {
            ProcessedUpdateIds = ProcessedUpdateIds.Append(updateId).TakeLast(MaxProcessedIds).ToList(),
            Session = session.Clone(),
        };
}
