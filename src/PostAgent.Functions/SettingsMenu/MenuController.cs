using Microsoft.Extensions.Options;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;

namespace PostAgent.SettingsMenu;

// Turns a button press or a typed answer into a change of the settings and the next screen. The only stored state is the pending question.
public sealed class MenuController(
    SettingsEditor editor,
    IEffectiveOptions effective,
    IOverridesStore store,
    IOptions<AgentOptions> app,
    ITelegramPanel panel,
    TimeProvider time,
    ILogger<MenuController> log)
{
    private static readonly TimeSpan PendingLifetime = TimeSpan.FromMinutes(10);

    // Writes the "-" the quiet-hours question accepts as "off".
    private const string QuietHoursOff = "-";

    private const string SourcesKey = "Sources";

    public async Task HandleCommandAsync(long chatId, string text, CancellationToken ct)
    {
        await ClearPendingAsync(chatId, ct);

        if (CommandName(text) == "/cancel")
        {
            await panel.SendAsync(chatId, SettingsMenuTexts.Cancelled, InlineKeyboard.Empty, ct);
            return;
        }

        var screen = await HomeAsync(ct);
        await panel.SendAsync(chatId, screen.Html, screen.Keyboard, ct);
    }

    public async Task HandleCallbackAsync(long chatId, int messageId, MenuAction action, CancellationToken ct)
    {
        var screen = await ActAsync(action, chatId, messageId, ct);
        if (screen is not null)
        {
            await ShowAsync(chatId, messageId, screen, ct);
        }
    }

    // False when the text is not an answer to a pending question (none, or it expired), so the chat gets it as a question.
    public async Task<bool> HandleTextAsync(long chatId, string text, CancellationToken ct)
    {
        var pending = (await store.LoadAsync(ct))?.Value.Pending;
        if (pending is null || !pending.IsFor(chatId) || pending.ExpiresAt <= time.GetUtcNow())
        {
            return false;
        }

        if (pending.Key == SourcesKey)
        {
            return await HandleSourceTextAsync(chatId, pending, text, ct);
        }

        if (SettingsCatalog.Find(pending.Key) is not { } definition)
        {
            await ClearPendingAsync(chatId, ct);
            return false;
        }

        var value = text.Trim();
        var result = await editor.SetValueAsync(pending.Key, definition.Kind == SettingKind.QuietHours && value == QuietHoursOff ? string.Empty : value, ct);
        if (!result.Ok)
        {
            await ShowAsync(chatId, pending.MessageId, MenuScreens.Ask(definition, result.Error), ct);
            return true;
        }

        await ClearPendingAsync(chatId, ct);
        await ShowAsync(chatId, pending.MessageId, await GroupAsync(definition.Group, SettingsMenuTexts.Saved, ct), ct);
        return true;
    }

    // Only the question of this chat: another chat's menu must not cancel it.
    private Task<EditResult> ClearPendingAsync(long chatId, CancellationToken ct) =>
        editor.UpdateAsync(o => o.Pending is { } p && p.IsFor(chatId) ? o with { Pending = null } : o, validate: false, ct);

    private async Task<Screen?> ActAsync(MenuAction action, long chatId, int messageId, CancellationToken ct)
    {
        switch (action.Kind)
        {
            case MenuActionKind.Main:
            case MenuActionKind.Cancel:
                await ClearPendingAsync(chatId, ct);
                return await HomeAsync(ct);

            case MenuActionKind.ResetAll:
                await editor.ResetAllAsync(ct);
                return await HomeAsync(ct);

            case MenuActionKind.OpenGroup when Enum.TryParse<SettingGroup>(action.Arg, out var group):
                await ClearPendingAsync(chatId, ct);
                return group == SettingGroup.Sources ? await SourcesAsync(null, ct) : await GroupAsync(group, null, ct);

            case MenuActionKind.OpenSources:
                await ClearPendingAsync(chatId, ct);
                return await SourcesAsync(null, ct);

            case MenuActionKind.AddSource:
                await editor.SetPendingAsync(new PendingInput(SourcesKey, "add-source", messageId, time.GetUtcNow() + PendingLifetime, chatId), ct);
                return MenuScreens.AskSource(null);

            case MenuActionKind.RemoveSource:
                return await AfterSourceEditAsync(await editor.RemoveSourceAsync(action.Arg, ct), ct);

            case MenuActionKind.ToggleAlert:
                return await AfterSourceEditAsync(await editor.ToggleAlertSourceAsync(action.Arg, ct), ct);

            case MenuActionKind.OpenChoice when SettingsCatalog.Find(action.Arg) is { Kind: SettingKind.Choice } definition:
                return MenuScreens.Choice(definition, await effective.GetFreshAsync(ct));

            case MenuActionKind.SetInt when SettingsCatalog.Find(action.Arg) is { Kind: SettingKind.Int } definition:
                return await AfterSetAsync(definition, action.Number.ToString(), ct);

            case MenuActionKind.Choose when SettingsCatalog.Find(action.Arg) is { Kind: SettingKind.Choice } definition
                && action.Number >= 0 && action.Number < definition.Choices.Count:
                return await AfterSetAsync(definition, definition.Choices[action.Number].Value, ct);

            case MenuActionKind.AskText when SettingsCatalog.Find(action.Arg) is { Kind: SettingKind.Text or SettingKind.QuietHours } definition:
                await editor.SetPendingAsync(new PendingInput(definition.Key, "set", messageId, time.GetUtcNow() + PendingLifetime, chatId), ct);
                return MenuScreens.Ask(definition, null);

            case MenuActionKind.Reset when SettingsCatalog.Find(action.Arg) is { } definition:
                await editor.ResetAsync(definition.Key, ct);
                return await GroupAsync(definition.Group, null, ct);

            case MenuActionKind.ResetGroup when Enum.TryParse<SettingGroup>(action.Arg, out var group):
                await editor.ResetGroupAsync(group, ct);
                return group == SettingGroup.Sources ? await SourcesAsync(null, ct) : await GroupAsync(group, null, ct);

            case MenuActionKind.ApplyProposal:
                return await ApplyProposalAsync(action.Arg, chatId, ct);

            case MenuActionKind.DeclineProposal:
                return await editor.DeclineProposalAsync(action.Arg, chatId, ct) ? new Screen(SettingsProposalMessage.Declined, InlineKeyboard.Empty) : null;

            default:
                // An unknown key or a value outside the catalog (an old or forged button): nothing changes.
                return null;
        }
    }

    // A second tap finds the proposal gone and changes nothing, so it cannot overwrite the "applied" text.
    private async Task<Screen?> ApplyProposalAsync(string id, long chatId, CancellationToken ct)
    {
        var outcome = await editor.ApplyProposalAsync(id, chatId, time.GetUtcNow(), ct);
        return outcome.Status switch
        {
            ProposalStatus.Applied => new Screen(SettingsProposalMessage.Applied(outcome.Proposal!), InlineKeyboard.Empty),
            ProposalStatus.Expired => new Screen(SettingsProposalMessage.Expired, InlineKeyboard.Empty),
            ProposalStatus.Failed => new Screen(SettingsProposalMessage.Failed(outcome.Error), InlineKeyboard.Empty),
            _ => null,
        };
    }

    private async Task<Screen> AfterSetAsync(SettingDefinition definition, string value, CancellationToken ct)
    {
        var result = await editor.SetValueAsync(definition.Key, value, ct);
        var note = result.Ok ? null : "⚠️ " + TelegramHtml.Escape(result.Error ?? string.Empty);
        return await GroupAsync(definition.Group, note, ct);
    }

    private async Task<Screen> GroupAsync(SettingGroup group, string? note, CancellationToken ct)
    {
        var options = await effective.GetFreshAsync(ct);
        var overrides = (await store.LoadAsync(ct))?.Value ?? SettingsOverrides.Empty;
        return MenuScreens.Group(group, options, overrides, note);
    }

    private async Task<bool> HandleSourceTextAsync(long chatId, PendingInput pending, string text, CancellationToken ct)
    {
        if (SourceRules.TryParse(text) is not { } name)
        {
            await ShowAsync(chatId, pending.MessageId, MenuScreens.AskSource(SettingsMenuTexts.BadSourceName), ct);
            return true;
        }

        var result = await editor.AddSourceAsync(name, ct);
        if (!result.Ok)
        {
            await ShowAsync(chatId, pending.MessageId, MenuScreens.AskSource(result.Error), ct);
            return true;
        }

        await ClearPendingAsync(chatId, ct);
        await ShowAsync(chatId, pending.MessageId, await SourcesAsync(SettingsMenuTexts.SourceAdded, ct), ct);
        return true;
    }

    private async Task<Screen> AfterSourceEditAsync(EditResult result, CancellationToken ct) =>
        await SourcesAsync(result.Ok ? null : "⚠️ " + TelegramHtml.Escape(result.Error ?? string.Empty), ct);

    private async Task<Screen> SourcesAsync(string? note, CancellationToken ct)
    {
        var overrides = (await store.LoadAsync(ct))?.Value ?? SettingsOverrides.Empty;
        var sources = overrides.Sources ?? app.Value.SourceList.Select(name => new SourceEntry(name, true)).ToList();
        var alerts = overrides.AlertSources ?? app.Value.AlertSourceSet.ToList();
        return MenuScreens.Sources(sources, alerts, note, fromBot: overrides.Sources is not null || overrides.AlertSources is not null);
    }

    // The bot's values are ignored when the blob cannot be read, or when together they make the options invalid (see EffectiveOptions).
    private async Task<Screen> HomeAsync(CancellationToken ct)
    {
        var handle = await store.LoadAsync(ct);
        var unreadable = handle is { Unreadable: true };
        var ignored = unreadable || (handle is not null && OverridesApplier.Check(OverridesApplier.Apply(app.Value, handle.Value)).Count > 0);
        return MenuScreens.Home(ignored, unreadable);
    }

    // The menu message may be gone or older than 48 hours: then the screen goes out as a new message.
    private async Task ShowAsync(long chatId, int messageId, Screen screen, CancellationToken ct)
    {
        try
        {
            await panel.EditAsync(chatId, messageId, screen.Html, screen.Keyboard, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogInformation("The menu message could not be edited, sending a new one: {Reason}", ex.GetType().Name);
            await panel.SendAsync(chatId, screen.Html, screen.Keyboard, ct);
        }
    }

    public static string CommandName(string text) => text.Trim().Split(' ', 2)[0].Split('@', 2)[0].ToLowerInvariant();
}
