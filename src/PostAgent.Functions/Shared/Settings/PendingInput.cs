namespace PostAgent.Shared.Settings;

// The one question the bot waits an answer to: which setting, what to do with the typed text, and which menu message to edit.
// ChatId is the chat that was asked (0 in a blob written before ADR-0014: any chat).
public sealed record PendingInput(string Key, string Action, int MessageId, DateTimeOffset ExpiresAt, long ChatId = 0)
{
    public bool IsFor(long chatId) => ChatId == 0 || ChatId == chatId;
}
