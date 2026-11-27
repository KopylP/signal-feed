using PostAgent.Shared.Chat;

namespace PostAgent.Shared.Settings;

// The agent's plans (ADR-0013): checked while the agent prepares them, stored until the owner taps a button, then applied or declined.
// The settings part is written through SettingsEditor; the reader profile and the conversation are their own blobs.
public sealed class SettingsProposals(
    SettingsEditor editor,
    OverridesChanges changes,
    IOverridesStore store,
    IProfileStore profile,
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

        if (!changes.TryApplyAll(handle?.Value ?? SettingsOverrides.Empty, plan, out _, out var error))
        {
            return EditResult.Fail(error!);
        }

        return await EditProfileAsync(plan, chatId, ct) is { Error: { } profileError } ? EditResult.Fail(profileError) : EditResult.Success;
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

        // A profile edit is written after the proposal is taken out, so a tap never applies it twice. It is checked first: a stale edit changes nothing.
        var planned = (await store.LoadAsync(ct))?.Value.Proposals?.FirstOrDefault(p => p.Id == id && p.IsFor(chatId));
        var profileEdit = planned is null || planned.ExpiresAt <= now
            ? ProfileEdit.None
            : await EditProfileAsync(planned.Changes, chatId, ct);
        if (profileEdit.Error is not null)
        {
            return new ProposalOutcome(ProposalStatus.Failed, planned, profileEdit.Error);
        }

        var result = await editor.UpdateAsync(
            o =>
            {
                found = o.Proposals?.FirstOrDefault(p => p.Id == id && p.IsFor(chatId));
                error = null;
                if (found is null || found.ExpiresAt <= now)
                {
                    return o;
                }

                // The proposal was replaced between the check of the profile edit and this write: the text we hold is for the old one.
                if (profileEdit.Text is not null && planned is not null && !found.Changes.SequenceEqual(planned.Changes))
                {
                    error = "Пропозиція змінилась. Попроси агента ще раз.";
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
            _ when profileEdit.Text is not null && !await TrySaveProfileAsync(chatId, profileEdit.Handle, profileEdit.Text, ct) =>
                new ProposalOutcome(ProposalStatus.Failed, found, "Не вдалося зберегти профіль, попроси агента ще раз."),
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

    // What the profile becomes with the profile edits of a plan, checked against the text as it is now.
    private async Task<ProfileEdit> EditProfileAsync(IReadOnlyList<SettingChange> plan, long chatId, CancellationToken ct)
    {
        var edits = plan.Where(c => c.Kind == SettingChangeKind.EditProfile).ToList();
        if (edits.Count == 0)
        {
            return ProfileEdit.None;
        }

        var handle = await profile.LoadAsync(chatId, ct);
        var text = handle?.Text ?? string.Empty;
        foreach (var edit in edits)
        {
            if (!TextEdit.TryApply(text, edit.Before, edit.Value, out text, out var error))
            {
                return new ProfileEdit(null, error, handle);
            }
        }

        return new ProfileEdit(text, null, handle);
    }

    // A storage error is reported like a lost race: the proposal is already taken out, so the owner must hear that nothing was changed.
    private async Task<bool> TrySaveProfileAsync(long chatId, ProfileHandle? handle, string text, CancellationToken ct)
    {
        try
        {
            return await profile.TrySaveAsync(chatId, handle, text, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "The reader profile could not be saved");
            return false;
        }
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
