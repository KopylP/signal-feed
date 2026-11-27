using PostAgent.Shared.Chat;

namespace PostAgent.Shared.Settings;

// The agent's plans (ADR-0013): checked while the agent prepares them, stored until the owner taps a button, then applied or declined.
// The settings part is written through SettingsEditor; the conversation is its own blob.
public sealed class SettingsProposals(
    SettingsEditor editor,
    OverridesChanges changes,
    IOverridesStore store,
    IChatStateStore chats,
    ILogger<SettingsProposals> log)
{
    private const int MaxStored = 5;

    // A dry run for a plan the agent wants to propose: nothing is saved.
    public async Task<EditResult> CheckAsync(IReadOnlyList<SettingChange> plan, long chatId, CancellationToken ct)
    {
        var handle = await store.LoadAsync(ct);
        if (handle is { Unreadable: true })
        {
            return EditResult.Fail(SettingsEditor.UnreadableMessage);
        }

        return changes.TryApplyAll(handle?.Value ?? SettingsOverrides.Empty, plan, out _, out var error) ? EditResult.Success : EditResult.Fail(error!);
    }

    public Task<EditResult> AddAsync(SettingsProposal proposal, CancellationToken ct) =>
        editor.UpdateAsync(
            o => o with { Proposals = (o.Proposals ?? []).Where(p => p.Id != proposal.Id).Append(proposal).TakeLast(MaxStored).ToList() },
            validate: false,
            ct);

    // The owner pressed "apply": all the changes go in as one write, checked as a whole, and the proposal is removed with them.
    public async Task<ProposalOutcome> ApplyAsync(string id, long chatId, DateTimeOffset now, CancellationToken ct)
    {
        SettingsProposal? found = null;
        string? error = null;

        var result = await editor.UpdateAsync(
            o =>
            {
                found = o.Proposals?.FirstOrDefault(p => p.Id == id && p.IsFor(chatId));
                error = null;
                if (found is null || found.ExpiresAt <= now)
                {
                    return o;
                }

                if (!changes.TryApplyAll(o, found.Changes, out var updated, out error))
                {
                    return o;
                }

                return updated with { Proposals = o.Proposals!.Where(p => p.Id != id).ToList() };
            },
            validate: false,
            ct);

        if (!result.Ok)
        {
            return new ProposalOutcome(ProposalStatus.Failed, found, result.Error);
        }

        return found switch
        {
            null => new ProposalOutcome(ProposalStatus.Gone),
            _ when found.ExpiresAt <= now => new ProposalOutcome(ProposalStatus.Expired, found),
            _ when error is not null => new ProposalOutcome(ProposalStatus.Failed, found, error),
            _ when found.Changes.Any(c => c.Kind == SettingChangeKind.ClearContext) && !await TryClearConversationAsync(chatId, ct) =>
                new ProposalOutcome(ProposalStatus.Failed, found, "Не вдалося очистити контекст, спробуй /clear."),
            _ => new ProposalOutcome(ProposalStatus.Applied, found),
        };
    }

    // False when there was nothing to decline (a second tap).
    public async Task<bool> DeclineAsync(string id, long chatId, CancellationToken ct)
    {
        var existed = false;
        var result = await editor.UpdateAsync(
            o =>
            {
                existed = o.Proposals?.Any(p => p.Id == id && p.IsFor(chatId)) ?? false;
                return existed ? o with { Proposals = o.Proposals!.Where(p => p.Id != id).ToList() } : o;
            },
            validate: false,
            ct);

        return result.Ok && existed;
    }

    private async Task<bool> TryClearConversationAsync(long chatId, CancellationToken ct)
    {
        try
        {
            return await chats.ClearConversationAsync(chatId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "The conversation could not be cleared");
            return false;
        }
    }
}
