using Microsoft.Extensions.Options;
using PostAgent.Shared.Topics;

namespace PostAgent.Shared.Settings;

// Every change to the overrides blob goes through here: read, change, validate, save with the ETag, retry on a conflict.
// What a change does is OverridesChanges; the agent's plans are SettingsProposals; the menu's own state is MenuState.
public sealed class SettingsEditor(
    IOverridesStore store,
    IOptions<AgentOptions> app,
    IEffectiveOptions effective,
    OverridesChanges changes,
    ITopicsToFollowQueue topicsToFollow,
    ILogger<SettingsEditor> log)
{
    public const string UnreadableMessage = "Збережені налаштування не вдалося прочитати. Скинь їх у головному меню.";

    private const int MaxAttempts = 3;

    public Task<EditResult> SetValueAsync(string key, string value, CancellationToken ct) =>
        ChangeAsync(new SettingChange(SettingChangeKind.Set, key, value), validate: true, replaceUnreadable: false, ct);

    // Reset is never validated: it is the way out when a value from the bot turned out to be a problem.
    public Task<EditResult> ResetAsync(string key, CancellationToken ct) =>
        ChangeAsync(new SettingChange(SettingChangeKind.Reset, key), validate: false, replaceUnreadable: true, ct);

    // Everything the bot set goes back to the App Settings. The way out when the stored settings cannot be read.
    public Task<EditResult> ResetAllAsync(CancellationToken ct) =>
        UpdateCoreAsync(
            o => o with { Values = new Dictionary<string, string>(), Sources = null, AlertSources = null, Watches = null, Pending = null, PromptSections = null, Proposals = null },
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

    // Following a topic is not invasive (ADR-0021): the chat agent does it at once, with the same checks as a confirmed plan.
    public Task<EditResult> AddWatchAsync(string phrase, CancellationToken ct) =>
        ChangeAsync(new SettingChange(SettingChangeKind.AddWatch, phrase), validate: true, replaceUnreadable: false, ct);

    public Task<EditResult> RemoveWatchAsync(string phrase, CancellationToken ct) =>
        ChangeAsync(new SettingChange(SettingChangeKind.RemoveWatch, phrase), validate: true, replaceUnreadable: false, ct);

    // The sources and the alert sources that are in force now (the bot's list, or the App Settings one).
    public async Task<(IReadOnlyList<SourceEntry> Sources, IReadOnlyList<string> Alerts)> CurrentSourcesAsync(CancellationToken ct)
    {
        var current = await CurrentAsync(ct);
        return (current.EffectiveSources(app.Value), current.EffectiveAlertSources(app.Value));
    }

    // The topics that are followed now (the bot's list, or the App Setting one).
    public async Task<IReadOnlyList<string>> CurrentWatchesAsync(CancellationToken ct) =>
        (await CurrentAsync(ct)).EffectiveWatches(app.Value);

    // Sources that were added from the bot and have not been read yet.
    public async Task<IReadOnlyList<string>> UnverifiedSourcesAsync(CancellationToken ct) =>
        (await CurrentAsync(ct)).Sources?.Where(s => !s.Verified).Select(s => s.Name).ToList() ?? [];

    // The change must return the same instance when there is nothing to write.
    public Task<EditResult> UpdateAsync(Func<SettingsOverrides, SettingsOverrides> change, bool validate, CancellationToken ct) =>
        UpdateCoreAsync(change, validate, replaceUnreadable: false, ct);

    private async Task<SettingsOverrides> CurrentAsync(CancellationToken ct) => (await store.LoadAsync(ct))?.Value ?? SettingsOverrides.Empty;

    private async Task<EditResult> ChangeAsync(SettingChange change, bool validate, bool replaceUnreadable, CancellationToken ct)
    {
        string? error = null;
        var result = await UpdateCoreAsync(
            o =>
            {
                var updated = changes.Apply(o, change, out error);
                return error is null ? updated : o;
            },
            validate,
            replaceUnreadable,
            ct);

        return error is null ? result : EditResult.Fail(error);
    }

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

            if (validate && changes.Validate(updated) is { } problems)
            {
                return EditResult.Fail(problems);
            }

            if (await store.TrySaveAsync(handle, updated, ct))
            {
                effective.Invalidate();
                await PublishNewTopicsAsync(current, updated, ct);
                return EditResult.Success;
            }

            log.LogInformation("Settings changed by someone else, attempt {Attempt} of {Max}", attempt, MaxAttempts);
        }

        return EditResult.Fail("Не вдалося зберегти, спробуй ще раз.");
    }

    // A topic new to the list (added by the agent, or back after a reset) is published once, so FollowTopicFunction embeds it and announces
    // its recent matches. The settings are saved already: a failure here only leaves the topic matching by words.
    private async Task PublishNewTopicsAsync(SettingsOverrides before, SettingsOverrides after, CancellationToken ct)
    {
        var old = before.EffectiveWatches(app.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var phrase in after.EffectiveWatches(app.Value).Where(p => !old.Contains(p)))
        {
            try
            {
                await topicsToFollow.EnqueueAsync(phrase, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "A new followed topic was not queued for embedding, it matches by words only");
            }
        }
    }
}
