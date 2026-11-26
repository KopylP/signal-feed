namespace PostAgent.Shared.Settings;

// HandledUpdateIds are the last ids of the updates the menu claimed (a small set, never a watermark: Telegram may restart ids and deliver in parallel).
// Proposals are the agent's plans that wait for a button press (the last few).
// PromptSections holds the owner's text for the editable sections of the prompts (section id to text); null means the shipped prompts.
// What the owner set from the Telegram bot. A null list means "not overridden, use the App Setting".
public sealed record SettingsOverrides(
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyList<SourceEntry>? Sources,
    IReadOnlyList<string>? AlertSources,
    PendingInput? Pending,
    IReadOnlyList<long> HandledUpdateIds,
    IReadOnlyList<SettingsProposal>? Proposals = null,
    IReadOnlyDictionary<string, string>? PromptSections = null)
{
    public static readonly SettingsOverrides Empty = new(new Dictionary<string, string>(), null, null, null, []);
}
