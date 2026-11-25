using System.ComponentModel;
using System.Globalization;
using System.Text;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Infrastructure;

// The tools that let the agent prepare a change of the settings. They only propose: each one checks the change and adds it to the plan of this turn,
// and nothing is stored or applied until the owner presses a button under a message that the code builds from the plan (SettingsProposalSender).
public sealed class SettingsTools(SettingsEditor editor, IEffectiveOptions effective, List<SettingChange> draft)
{
    private const string Proposed =
        "Added to the plan. Nothing is changed yet: the system will show the plan with a confirm button. Do not say it is applied.";

    [Description("List the settings the reader can change (key, label, current value, allowed range or choices) and the current sources. Call it before proposing, and to show the reader a list to pick from.")]
    public async Task<string> GetSettings(CancellationToken ct)
    {
        var options = await effective.GetFreshAsync(ct);
        var text = new StringBuilder();
        foreach (var group in SettingsCatalog.All.GroupBy(d => d.Group))
        {
            text.Append('[').Append(group.Key).AppendLine("]");
            foreach (var d in group)
            {
                text.Append(d.Key).Append(" (").Append(d.Label).Append("): ").Append(SettingsCatalog.Display(d, SettingsCatalog.Raw(d, options))).Append(Allowed(d)).AppendLine();
            }
        }

        var (sources, alerts) = await editor.CurrentSourcesAsync(ct);
        text.AppendLine("[Sources]");
        foreach (var source in sources)
        {
            text.Append('@').Append(source.Name).Append(alerts.Contains(source.Name) ? " (alerts on)" : " (alerts off)").AppendLine();
        }

        return text.ToString();
    }

    [Description("Plan to set one setting to a value. Use the key from get_settings. Integers as digits, a choice as its exact value, quiet hours as 23:00-07:00 or - to turn them off.")]
    public Task<string> ProposeSet(
        [Description("The setting key, like DigestSize.")] string key,
        [Description("The new value.")] string value,
        CancellationToken ct)
    {
        var normalized = SettingsCatalog.Find(key) is { Kind: SettingKind.QuietHours } && value.Trim() is "-" or "off"
            ? string.Empty
            : value.Trim();
        return AddAsync(new SettingChange(SettingChangeKind.Set, key.Trim(), normalized), ct);
    }

    [Description("Plan to put one setting back to the value from the app settings.")]
    public Task<string> ProposeReset([Description("The setting key.")] string key, CancellationToken ct) =>
        SettingsCatalog.Find(key.Trim()) is null
            ? Task.FromResult("Unknown setting key. Call get_settings.")
            : AddAsync(new SettingChange(SettingChangeKind.Reset, key.Trim()), ct);

    [Description("Plan to put a whole group of settings (Sources, Digest, Alerts, Collecting, Chat, Models) back to the values from the app settings.")]
    public Task<string> ProposeResetGroup([Description("The group name.")] string group, CancellationToken ct) =>
        Enum.TryParse<SettingGroup>(group.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? AddAsync(new SettingChange(SettingChangeKind.ResetGroup, parsed.ToString()), ct)
            : Task.FromResult("Unknown group. Use one of: " + string.Join(", ", Enum.GetNames<SettingGroup>()));

    [Description("Plan to add a Telegram channel or group as a source.")]
    public Task<string> ProposeAddSource([Description("The username, like @name or t.me/name.")] string name, CancellationToken ct) =>
        SourceRules.TryParse(name) is { } parsed
            ? AddAsync(new SettingChange(SettingChangeKind.AddSource, parsed), ct)
            : Task.FromResult("This is not a valid channel name (5-32 letters, digits and underscores).");

    [Description("Plan to remove a source. The name must be one of the current sources from get_settings; if the reader was not specific, ask which one.")]
    public Task<string> ProposeRemoveSource([Description("The source name.")] string name, CancellationToken ct) =>
        ExistingSourceAsync(name, SettingChangeKind.RemoveSource, ct);

    [Description("Plan to switch the alerts for a source on or off (it flips the current state). The name must be one of the current sources from get_settings.")]
    public Task<string> ProposeToggleAlertSource([Description("The source name.")] string name, CancellationToken ct) =>
        ExistingSourceAsync(name, SettingChangeKind.ToggleAlertSource, ct);

    private async Task<string> ExistingSourceAsync(string name, SettingChangeKind kind, CancellationToken ct)
    {
        var normalized = SourceNames.Normalize(name);
        var (sources, _) = await editor.CurrentSourcesAsync(ct);
        return sources.Any(s => s.Name == normalized)
            ? await AddAsync(new SettingChange(kind, normalized), ct)
            : "There is no such source. Call get_settings and ask the reader which one they mean.";
    }

    // The whole plan is checked again with the new step, so the model hears about a problem now and can correct it.
    private async Task<string> AddAsync(SettingChange change, CancellationToken ct)
    {
        var plan = draft.Where(c => c.Kind != change.Kind || c.Target != change.Target).Append(change).ToList();
        if (plan.Count > SettingsProposal.MaxChanges)
        {
            return $"Too many changes in one plan (at most {SettingsProposal.MaxChanges.ToString(CultureInfo.InvariantCulture)}). Ask the reader to do the rest separately.";
        }

        var check = await editor.CheckAsync(plan, ct);
        if (!check.Ok)
        {
            return "Rejected, not added to the plan: " + check.Error;
        }

        draft.Clear();
        draft.AddRange(plan);
        return Proposed;
    }

    private static string Allowed(SettingDefinition d) => d.Kind switch
    {
        SettingKind.Int => $" [{d.Min.ToString(CultureInfo.InvariantCulture)}..{d.Max.ToString(CultureInfo.InvariantCulture)}]",
        SettingKind.Choice => " [" + string.Join(" | ", d.Choices.Select(c => c.Value)) + "]",
        SettingKind.QuietHours => " [HH:mm-HH:mm or empty]",
        _ => string.Empty,
    };
}
