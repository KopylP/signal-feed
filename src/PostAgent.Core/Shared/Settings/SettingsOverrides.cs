namespace PostAgent.Shared.Settings;

// HandledUpdateIds are the last ids of the updates the menu claimed (a small set, never a watermark: Telegram may restart ids and deliver in parallel).
// Proposals are the agent's plans that wait for a button press (the last few).
// Watches are the topics the owner follows (WatchPhrases); null means the App Setting.
// PromptSections holds the owner's text for the editable sections of the prompts (section id to text); null means the shipped prompts.
// What the owner set from the Telegram bot. A null list means "not overridden, use the App Setting".
public sealed record SettingsOverrides(
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyList<SourceEntry>? Sources,
    IReadOnlyList<string>? AlertSources,
    PendingInput? Pending,
    IReadOnlyList<long> HandledUpdateIds,
    IReadOnlyList<SettingsProposal>? Proposals = null,
    IReadOnlyDictionary<string, string>? PromptSections = null,
    IReadOnlyList<string>? Watches = null)
{
    public static readonly SettingsOverrides Empty = new(new Dictionary<string, string>(), null, null, null, []);

    // Until the bot changes a list, the App Setting one is in force; its sources count as verified.
    public IReadOnlyList<SourceEntry> EffectiveSources(AgentOptions app) =>
        Sources ?? app.SourceList.Select(name => new SourceEntry(name, true)).ToList();

    public IReadOnlyList<string> EffectiveAlertSources(AgentOptions app) => AlertSources ?? app.AlertSourceSet.ToList();

    public IReadOnlyList<string> EffectiveWatches(AgentOptions app) => Watches ?? app.WatchList.ToList();
}
