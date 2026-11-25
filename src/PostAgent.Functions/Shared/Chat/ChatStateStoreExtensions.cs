namespace PostAgent.Shared.Chat;

public static class ChatStateStoreExtensions
{
    private const int MaxAttempts = 3;

    // Forgets the conversation of the chat and keeps its digest. Nothing to forget is not an error. A turn that is running can write in between,
    // so it retries a few times, like every writer of this blob.
    public static async Task<bool> ClearConversationAsync(this IChatStateStore store, long chatId, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var current = await store.LoadAsync(chatId, ct);
            if (current is null || current.State.Session is null)
            {
                return true;
            }

            if (await store.TrySaveAsync(chatId, current, current.State.WithoutSession(), ct))
            {
                return true;
            }
        }

        return false;
    }
}
