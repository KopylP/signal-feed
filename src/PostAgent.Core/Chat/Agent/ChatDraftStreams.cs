namespace PostAgent.Chat.Agent;

// Opens the draft stream of one answer.
public sealed class ChatDraftStreams(IChatDrafts drafts, TimeProvider time, ILogger<ChatDraftStream> log)
{
    public ChatDraftStream Open(long chatId) => new(drafts, time, log, chatId, Random.Shared.NextInt64(1, int.MaxValue));
}
