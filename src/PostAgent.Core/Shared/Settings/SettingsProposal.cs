namespace PostAgent.Shared.Settings;

// A plan of changes that the agent prepared and the owner has not confirmed yet. Id is the Telegram update id of the question
// that led to it, so a redelivered question replaces its proposal instead of adding a second one.
// ChatId is the chat that got it and the only one that can apply it (0 in a blob written before ADR-0014: any chat).
public sealed record SettingsProposal(string Id, IReadOnlyList<SettingChange> Changes, DateTimeOffset ExpiresAt, long ChatId = 0)
{
    public const int MaxChanges = 8;

    public bool IsFor(long chatId) => ChatId == 0 || ChatId == chatId;

    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
}
