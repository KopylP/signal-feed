using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat;

// Stores the plan the agent prepared and sends it with the confirm button. The text comes from SettingsProposalMessage, not from the model.
// Never throws: the answer is already saved, and a plan that did not arrive is simply asked for again.
public sealed class SettingsProposalSender(
    SettingsEditor editor,
    IEffectiveOptions effective,
    ITelegramPanel panel,
    ChatResolver resolver,
    TimeProvider time,
    ILogger<SettingsProposalSender> log)
{
    public async Task SendAsync(long updateId, IReadOnlyList<SettingChange> changes, CancellationToken ct)
    {
        try
        {
            if (await resolver.TryGetChatIdAsync(ct) is not { } chatId)
            {
                return;
            }

            var proposal = new SettingsProposal(updateId.ToString(System.Globalization.CultureInfo.InvariantCulture), changes, time.GetUtcNow() + SettingsProposal.Lifetime);
            var saved = await editor.ProposeAsync(proposal, ct);
            if (!saved.Ok)
            {
                await panel.SendAsync(chatId, SettingsProposalMessage.NotSaved(saved.Error), InlineKeyboard.Empty, ct);
                return;
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
