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

    public async Task<EditResult> SetValueAsync(string key, string value, CancellationToken ct)
    {
        if (SettingsCatalog.Find(key) is not { } definition)
        {
            return EditResult.Fail("Невідомий параметр.");
        }

        if (definition.Kind == SettingKind.Int && !int.TryParse(value, out _))
        {
            return EditResult.Fail("Потрібне число.");
        }

        if (definition.Kind == SettingKind.Choice && definition.Choices.All(c => c.Value != value))
        {
            return EditResult.Fail("Такого варіанта немає.");
        }

        return await UpdateAsync(
            o => o with { Values = new Dictionary<string, string>(o.Values) { [key] = value } },
            validate: true,
            ct);
    }

    // Reset is never validated: it is the way out when a value from the bot turned out to be a problem.
    public Task<EditResult> ResetAsync(string key, CancellationToken ct) =>
        UpdateCoreAsync(o => o with { Values = o.Values.Where(v => v.Key != key).ToDictionary() }, validate: false, replaceUnreadable: true, ct);

    // Everything the bot set goes back to the App Settings. The way out when the stored settings cannot be read.
    public Task<EditResult> ResetAllAsync(CancellationToken ct) =>
        UpdateCoreAsync(
            o => o with { Values = new Dictionary<string, string>(), Sources = null, AlertSources = null, Pending = null },
            validate: false,
            replaceUnreadable: true,
            ct);

    public Task<EditResult> ResetGroupAsync(SettingGroup group, CancellationToken ct)
    {
        var keys = SettingsCatalog.InGroup(group).Select(d => d.Key).ToHashSet();
        return UpdateCoreAsync(
            o => o with
            {
                Values = o.Values.Where(v => !keys.Contains(v.Key)).ToDictionary(),
                Sources = group == SettingGroup.Sources ? null : o.Sources,
                AlertSources = group == SettingGroup.Sources ? null : o.AlertSources,
            },
            validate: false,
            replaceUnreadable: true,
            ct);
    }

    // The first read that ends without an error confirms a source added from the bot. Sources from App Settings need no mark.
    public async Task MarkSourceVerifiedAsync(string name, CancellationToken ct) =>
        await UpdateAsync(
            o => o.Sources is { } sources && sources.Any(s => s.Name == name && !s.Verified)
                ? o with { Sources = sources.Select(s => s.Name == name ? s with { Verified = true } : s).ToList() }
                : o,
            validate: false,
            ct);

    public Task<EditResult> AddSourceAsync(string name, CancellationToken ct) =>
        UpdateAsync(
            o => SourcesOf(o).Any(s => s.Name == name) ? o : o with { Sources = [.. SourcesOf(o), new SourceEntry(name, false)] },
            validate: true,
            ct);

    public async Task<EditResult> RemoveSourceAsync(string name, CancellationToken ct)
    {
        string? refusal = null;
        var result = await UpdateAsync(
            o =>
            {
                var sources = SourcesOf(o);
                if (sources.All(s => s.Name != name))
                {
                    return o;
                }

                if (sources.Count == 1)
                {
                    refusal = "Не можна видалити останнє джерело.";
                    return o;
                }

                var alerts = AlertsOf(o);
                return o with
                {
                    Sources = sources.Where(s => s.Name != name).ToList(),
                    AlertSources = alerts.Contains(name) ? alerts.Where(a => a != name).ToList() : o.AlertSources,
                };
            },
            validate: true,
            ct);

        return refusal is null ? result : EditResult.Fail(refusal);
    }

    public Task<EditResult> ToggleAlertSourceAsync(string name, CancellationToken ct) =>
        UpdateAsync(
            o =>
            {
                if (SourcesOf(o).All(s => s.Name != name))
                {
                    return o;
                }

                var alerts = AlertsOf(o);
                return o with { AlertSources = alerts.Contains(name) ? alerts.Where(a => a != name).ToList() : [.. alerts, name] };
            },
            validate: false,
            ct);

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
                return EditResult.Fail("Збережені налаштування не вдалося прочитати. Скинь їх у головному меню.");
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
