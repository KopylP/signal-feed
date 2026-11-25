namespace PostAgent.Shared.Settings;

// The one question the bot waits an answer to: which setting, what to do with the typed text, and which menu message to edit.
public sealed record PendingInput(string Key, string Action, int MessageId, DateTimeOffset ExpiresAt);
