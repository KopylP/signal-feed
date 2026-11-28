using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Answering;

// Stores the plan the agent prepared and sends it with the confirm button. The text comes from SettingsProposalMessage, not from the model.
// Never throws: the answer is already saved, and a plan that did not arrive is simply asked for again.
public sealed class SettingsProposalSender(
    SettingsProposals proposals,
    IEffectiveOptions effective,
    IOverridesStore overrides,
    ITelegramPanel panel,
    TimeProvider time,
    ILogger<SettingsProposalSender> log)
{
    public async Task SendAsync(long chatId, long updateId, IReadOnlyList<SettingChange> changes, CancellationToken ct)
    {
        try
        {
            var proposal = new SettingsProposal(updateId.ToString(System.Globalization.CultureInfo.InvariantCulture), changes, time.GetUtcNow() + SettingsProposal.Lifetime, chatId);
            var saved = await proposals.AddAsync(proposal, ct);
            if (!saved.Ok)
            {
                await panel.SendAsync(chatId, SettingsProposalMessage.NotSaved(saved.Error), InlineKeyboard.Empty, ct);
                return;
            }

            // A rewrite is shown before the button, as the lines it changes against what the prompt has now.
            foreach (var diff in SettingsProposalMessage.RewriteDiffs(proposal, (await overrides.LoadAsync(ct))?.Value.PromptSections))
            {
                await panel.SendAsync(chatId, diff, InlineKeyboard.Empty, ct);
            }

            var html = SettingsProposalMessage.Proposed(proposal, await effective.GetFreshAsync(ct));
            await panel.SendAsync(chatId, html, SettingsProposalMessage.Keyboard(proposal), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "A settings proposal of {Count} changes was not sent", changes.Count);
        }
    }
}
