using System.Globalization;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Infrastructure.Tools;

// The changes the agent prepared while answering one question. They are only checked here; they are stored and shown by SettingsProposalSender,
// and applied by the owner's button.
public sealed class ProposalPlan(SettingsEditor editor, List<SettingChange> draft, long chatId)
{
    public const string Proposed =
        "Added to the plan. Nothing is changed yet: the system will show the plan with a confirm button. Do not say it is applied.";

    // The chat that asked: the reader profile and the confirmation belong to it.
    public long ChatId => chatId;

    // The whole plan is checked again with the new step, so the model hears about a problem now and can correct it.
    public async Task<string> AddAsync(SettingChange change, CancellationToken ct)
    {
        // Text edits are steps of their own and are applied in order; any other change replaces an earlier one for the same target.
        var textEdit = change.Kind is SettingChangeKind.EditPrompt or SettingChangeKind.EditProfile;
        var plan = (textEdit ? draft : draft.Where(c => c.Kind != change.Kind || c.Target != change.Target)).Append(change).ToList();
        if (plan.Count > SettingsProposal.MaxChanges)
        {
            return $"Too many changes in one plan (at most {SettingsProposal.MaxChanges.ToString(CultureInfo.InvariantCulture)}). Ask the reader to do the rest separately.";
        }

        var check = await editor.CheckAsync(plan, chatId, ct);
        if (!check.Ok)
        {
            return "Rejected, not added to the plan: " + check.Error;
        }

        draft.Clear();
        draft.AddRange(plan);
        return Proposed;
    }

    // For a change that needs a source that exists: a made-up name is sent back to the list instead of failing later.
    public async Task<string> AddForExistingSourceAsync(SettingChangeKind kind, string name, CancellationToken ct)
    {
        var normalized = SourceNames.Normalize(name);
        var (sources, _) = await editor.CurrentSourcesAsync(ct);
        return sources.Any(s => s.Name == normalized)
            ? await AddAsync(new SettingChange(kind, normalized), ct)
            : "There is no such source. Call get_settings and ask the reader which one they mean.";
    }
}
