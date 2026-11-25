namespace PostAgent.Shared.Settings;

// What the owner set from the Telegram bot. A null list means "not overridden, use the App Setting".
public sealed record SettingsOverrides(
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyList<SourceEntry>? Sources,
    IReadOnlyList<string>? AlertSources,
    PendingInput? Pending,
    long LastUpdateId)
{
    public static readonly SettingsOverrides Empty = new(new Dictionary<string, string>(), null, null, null, 0);
}
