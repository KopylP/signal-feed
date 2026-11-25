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
        UpdateAsync(o => o with { Values = o.Values.Where(v => v.Key != key).ToDictionary() }, validate: false, ct);

    public Task<EditResult> ResetGroupAsync(SettingGroup group, CancellationToken ct)
    {
        var keys = SettingsCatalog.InGroup(group).Select(d => d.Key).ToHashSet();
        return UpdateAsync(
            o => o with
            {
                Values = o.Values.Where(v => !keys.Contains(v.Key)).ToDictionary(),
                Sources = group == SettingGroup.Sources ? null : o.Sources,
                AlertSources = group == SettingGroup.Sources ? null : o.AlertSources,
            },
            validate: false,
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

    public Task<EditResult> SetPendingAsync(PendingInput? pending, CancellationToken ct) =>
        UpdateAsync(o => o.Pending == pending ? o : o with { Pending = pending }, validate: false, ct);

    // True when the id is newer than the last one handled and was recorded. False for a resent update (or when it could not be recorded).
    public async Task<bool> TryAdvanceUpdateIdAsync(long updateId, CancellationToken ct)
    {
        var advanced = false;
        var result = await UpdateAsync(
            o =>
            {
                advanced = updateId > o.LastUpdateId;
                return advanced ? o with { LastUpdateId = updateId } : o;
            },
            validate: false,
            ct);

        return result.Ok && advanced;
    }

    // The change must return the same instance when there is nothing to write.
    public async Task<EditResult> UpdateAsync(Func<SettingsOverrides, SettingsOverrides> change, bool validate, CancellationToken ct)
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
