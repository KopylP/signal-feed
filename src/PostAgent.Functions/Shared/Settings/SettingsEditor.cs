using Microsoft.Extensions.Options;

namespace PostAgent.Shared.Settings;

// Every change to the overrides blob goes through here: read, change, validate, save with the ETag, retry on a conflict.
public sealed class SettingsEditor(
    IOverridesStore store,
    IOptions<AgentOptions> app,
    IEffectiveOptions effective,
    ILogger<SettingsEditor> log)
{
    private const int MaxAttempts = 3;
    private const int MaxHandledIds = 30;
    private const int MaxProposals = 5;
    private const string UnreadableMessage = "Збережені налаштування не вдалося прочитати. Скинь їх у головному меню.";

    public Task<EditResult> SetValueAsync(string key, string value, CancellationToken ct) =>
        ChangeAsync(new SettingChange(SettingChangeKind.Set, key, value), validate: true, replaceUnreadable: false, ct);

    // Reset is never validated: it is the way out when a value from the bot turned out to be a problem.
    public Task<EditResult> ResetAsync(string key, CancellationToken ct) =>
        ChangeAsync(new SettingChange(SettingChangeKind.Reset, key), validate: false, replaceUnreadable: true, ct);

    // Everything the bot set goes back to the App Settings. The way out when the stored settings cannot be read.
    public Task<EditResult> ResetAllAsync(CancellationToken ct) =>
        UpdateCoreAsync(
            o => o with { Values = new Dictionary<string, string>(), Sources = null, AlertSources = null, Pending = null },
            validate: false,
            replaceUnreadable: true,
            ct);

    public Task<EditResult> ResetGroupAsync(SettingGroup group, CancellationToken ct) =>
        ChangeAsync(new SettingChange(SettingChangeKind.ResetGroup, group.ToString()), validate: false, replaceUnreadable: true, ct);

    // The first read that ends without an error confirms a source added from the bot. Sources from App Settings need no mark.
    public async Task MarkSourceVerifiedAsync(string name, CancellationToken ct) =>
        await UpdateAsync(
            o => o.Sources is { } sources && sources.Any(s => s.Name == name && !s.Verified)
                ? o with { Sources = sources.Select(s => s.Name == name ? s with { Verified = true } : s).ToList() }
                : o,
            validate: false,
            ct);

    public Task<EditResult> AddSourceAsync(string name, CancellationToken ct) =>
        ChangeAsync(new SettingChange(SettingChangeKind.AddSource, name), validate: true, replaceUnreadable: false, ct);

    public Task<EditResult> RemoveSourceAsync(string name, CancellationToken ct) =>
        ChangeAsync(new SettingChange(SettingChangeKind.RemoveSource, name), validate: true, replaceUnreadable: false, ct);

    public Task<EditResult> ToggleAlertSourceAsync(string name, CancellationToken ct) =>
        ChangeAsync(new SettingChange(SettingChangeKind.ToggleAlertSource, name), validate: false, replaceUnreadable: false, ct);

    // The sources and the alert sources that are in force now (the bot's list, or the App Settings one).
    public async Task<(IReadOnlyList<SourceEntry> Sources, IReadOnlyList<string> Alerts)> CurrentSourcesAsync(CancellationToken ct)
    {
        var current = (await store.LoadAsync(ct))?.Value ?? SettingsOverrides.Empty;
        return (SourcesOf(current), AlertsOf(current));
    }

    // A dry run for a plan the agent wants to propose: nothing is saved.
    public async Task<EditResult> CheckAsync(IReadOnlyList<SettingChange> changes, CancellationToken ct)
    {
        var handle = await store.LoadAsync(ct);
        if (handle is { Unreadable: true })
        {
            return EditResult.Fail(UnreadableMessage);
        }

        return TryApplyAll(handle?.Value ?? SettingsOverrides.Empty, changes, out _, out var error) ? EditResult.Success : EditResult.Fail(error!);
    }

    public Task<EditResult> ProposeAsync(SettingsProposal proposal, CancellationToken ct) =>
        UpdateAsync(
            o => o with { Proposals = (o.Proposals ?? []).Where(p => p.Id != proposal.Id).Append(proposal).TakeLast(MaxProposals).ToList() },
            validate: false,
            ct);

    // The owner pressed "apply": all the changes go in as one write, checked as a whole, and the proposal is removed with them.
    public async Task<ProposalOutcome> ApplyProposalAsync(string id, DateTimeOffset now, CancellationToken ct)
    {
        SettingsProposal? found = null;
        string? error = null;
        var result = await UpdateAsync(
            o =>
            {
                found = o.Proposals?.FirstOrDefault(p => p.Id == id);
                error = null;
                if (found is null || found.ExpiresAt <= now)
                {
                    return o;
                }

                if (!TryApplyAll(o, found.Changes, out var updated, out error))
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
            _ => new ProposalOutcome(ProposalStatus.Applied, found),
        };
    }

    // False when there was nothing to decline (a second tap).
    public async Task<bool> DeclineProposalAsync(string id, CancellationToken ct)
    {
        var existed = false;
        var result = await UpdateAsync(
            o =>
            {
                existed = o.Proposals?.Any(p => p.Id == id) ?? false;
                return existed ? o with { Proposals = o.Proposals!.Where(p => p.Id != id).ToList() } : o;
            },
            validate: false,
            ct);

        return result.Ok && existed;
    }

    // Sources that were added from the bot and have not been read yet.
    public async Task<IReadOnlyList<string>> UnverifiedSourcesAsync(CancellationToken ct) =>
        (await store.LoadAsync(ct))?.Value.Sources?.Where(s => !s.Verified).Select(s => s.Name).ToList() ?? [];

    public Task<EditResult> SetPendingAsync(PendingInput? pending, CancellationToken ct) =>
        UpdateAsync(o => o.Pending == pending ? o : o with { Pending = pending }, validate: false, ct);

    // True when the id was recorded as handled. False for a resent update, or when it could not be recorded.
    // A set of recent ids, not a watermark: Telegram delivers in parallel and may restart the numbering.
    public async Task<bool> TryMarkHandledAsync(long updateId, CancellationToken ct)
    {
        var marked = false;
        var result = await UpdateAsync(
            o =>
            {
                marked = !o.HandledUpdateIds.Contains(updateId);
                return marked ? o with { HandledUpdateIds = o.HandledUpdateIds.Append(updateId).TakeLast(MaxHandledIds).ToList() } : o;
            },
            validate: false,
            ct);

        return result.Ok && marked;
    }

    // Until the bot changes the list, the sources are the App Setting ones, and they count as verified.
    private List<SourceEntry> SourcesOf(SettingsOverrides o) =>
        o.Sources?.ToList() ?? app.Value.SourceList.Select(name => new SourceEntry(name, true)).ToList();

    private List<string> AlertsOf(SettingsOverrides o) => o.AlertSources?.ToList() ?? app.Value.AlertSourceSet.ToList();

    private async Task<EditResult> ChangeAsync(SettingChange change, bool validate, bool replaceUnreadable, CancellationToken ct)
    {
        string? error = null;
        var result = await UpdateCoreAsync(
            o =>
            {
                var updated = Transform(o, change, out error);
                return error is null ? updated : o;
            },
            validate,
            replaceUnreadable,
            ct);

        return error is null ? result : EditResult.Fail(error);
    }

    // All or nothing, and the result is checked as a whole. Resets are not checked: they are the way out of a bad state.
    private bool TryApplyAll(SettingsOverrides current, IReadOnlyList<SettingChange> changes, out SettingsOverrides updated, out string? error)
    {
        updated = current;
        error = null;
        foreach (var change in changes)
        {
            updated = Transform(updated, change, out error);
            if (error is not null)
            {
                return false;
            }
        }

        var onlyResets = changes.All(c => c.Kind is SettingChangeKind.Reset or SettingChangeKind.ResetGroup);
        if (!onlyResets && OverridesApplier.Check(OverridesApplier.Apply(app.Value, updated)) is { Count: > 0 } errors)
        {
            error = string.Join('\n', errors);
            return false;
        }

        return true;
    }

    // One change applied to the stored overrides. The same instance comes back when there is nothing to write.
    private SettingsOverrides Transform(SettingsOverrides o, SettingChange change, out string? error)
    {
        error = null;
        switch (change.Kind)
        {
            case SettingChangeKind.Set:
                return SetValue(o, change.Target, change.Value ?? string.Empty, out error);

            case SettingChangeKind.Reset:
                return o with { Values = o.Values.Where(v => v.Key != change.Target).ToDictionary() };

            case SettingChangeKind.ResetGroup when Enum.TryParse<SettingGroup>(change.Target, out var group) && Enum.IsDefined(group):
                var keys = SettingsCatalog.InGroup(group).Select(d => d.Key).ToHashSet();
                return o with
                {
                    Values = o.Values.Where(v => !keys.Contains(v.Key)).ToDictionary(),
                    Sources = group == SettingGroup.Sources ? null : o.Sources,
                    AlertSources = group == SettingGroup.Sources ? null : o.AlertSources,
                };

            case SettingChangeKind.AddSource:
                return SourcesOf(o).Any(s => s.Name == change.Target) ? o : o with { Sources = [.. SourcesOf(o), new SourceEntry(change.Target, false)] };

            case SettingChangeKind.RemoveSource:
                return RemoveSource(o, change.Target, out error);

            case SettingChangeKind.ToggleAlertSource:
                if (SourcesOf(o).All(s => s.Name != change.Target))
                {
                    return o;
                }

                var alerts = AlertsOf(o);
                return o with { AlertSources = alerts.Contains(change.Target) ? alerts.Where(a => a != change.Target).ToList() : [.. alerts, change.Target] };

            default:
                error = "Невідома зміна.";
                return o;
        }
    }

    private static SettingsOverrides SetValue(SettingsOverrides o, string key, string value, out string? error)
    {
        error = null;
        if (SettingsCatalog.Find(key) is not { } definition)
        {
            error = "Невідомий параметр.";
            return o;
        }

        if (definition.Kind == SettingKind.Int && !int.TryParse(value, out _))
        {
            error = "Потрібне число.";
            return o;
        }

        if (definition.Kind == SettingKind.Choice && definition.Choices.All(c => c.Value != value))
        {
            error = "Такого варіанта немає.";
            return o;
        }

        return o with { Values = new Dictionary<string, string>(o.Values) { [key] = value } };
    }

    private SettingsOverrides RemoveSource(SettingsOverrides o, string name, out string? error)
    {
        error = null;
        var sources = SourcesOf(o);
        if (sources.All(s => s.Name != name))
        {
            return o;
        }

        if (sources.Count == 1)
        {
            error = "Не можна видалити останнє джерело.";
            return o;
        }

        var alerts = AlertsOf(o);
        return o with
        {
            Sources = sources.Where(s => s.Name != name).ToList(),
            AlertSources = alerts.Contains(name) ? alerts.Where(a => a != name).ToList() : o.AlertSources,
        };
    }

    // The change must return the same instance when there is nothing to write.
    public Task<EditResult> UpdateAsync(Func<SettingsOverrides, SettingsOverrides> change, bool validate, CancellationToken ct) =>
        UpdateCoreAsync(change, validate, replaceUnreadable: false, ct);

    private async Task<EditResult> UpdateCoreAsync(
        Func<SettingsOverrides, SettingsOverrides> change, bool validate, bool replaceUnreadable, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var handle = await store.LoadAsync(ct);
            var current = handle?.Value ?? SettingsOverrides.Empty;
            var updated = change(current);
            if (ReferenceEquals(updated, current))
            {
                return EditResult.Success;
            }

            if (handle is { Unreadable: true } && !replaceUnreadable)
            {
                return EditResult.Fail(UnreadableMessage);
            }

            if (validate && OverridesApplier.Check(OverridesApplier.Apply(app.Value, updated)) is { Count: > 0 } errors)
            {
                return EditResult.Fail(string.Join('\n', errors));
            }

            if (await store.TrySaveAsync(handle, updated, ct))
            {
                effective.Invalidate();
                return EditResult.Success;
            }

            log.LogInformation("Settings changed by someone else, attempt {Attempt} of {Max}", attempt, MaxAttempts);
        }

        return EditResult.Fail("Не вдалося зберегти, спробуй ще раз.");
    }
}
