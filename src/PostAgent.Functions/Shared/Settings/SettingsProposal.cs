namespace PostAgent.Shared.Settings;

// A plan of changes that the agent prepared and the owner has not confirmed yet. Id is the Telegram update id of the question
// that led to it, so a redelivered question replaces its proposal instead of adding a second one.
public sealed record SettingsProposal(string Id, IReadOnlyList<SettingChange> Changes, DateTimeOffset ExpiresAt)
{
    public const int MaxChanges = 8;

    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
}
