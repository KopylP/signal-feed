using PostAgent.Shared.Chat;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;
using Telegram.Bot.Types;

namespace PostAgent.SettingsMenu;

// The first handler of the webhook: the settings menu, its buttons and the typed answer to its question. Everything else goes on to the chat.
public sealed class SettingsInbox(
    ChatResolver resolver,
    IOverridesStore store,
    MenuState menu,
    MenuController controller,
    ITelegramPanel panel,
    TimeProvider time,
    ILogger<SettingsInbox> log) : IIncomingMessageHandler
{
    public async Task<bool> HandleAsync(Update update, CancellationToken ct)
    {
        var chat = update.CallbackQuery?.Message?.Chat ?? update.Message?.Chat;
        if (chat is null || (await resolver.GetChatIdsAsync(ct)).Count == 0)
        {
            return false;
        }

        // Only a bound private chat: a button press from anywhere else is swallowed, a message goes on and is ignored by the chat as well.
        if (!await resolver.ServesAsync(chat, ct))
        {
            return update.CallbackQuery is not null;
        }

        OverridesHandle? handle;
        try
        {
            handle = await store.LoadAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The chat must keep working when the settings storage is down.
            log.LogWarning(ex, "Settings could not be loaded, the update is left to the others");
            return false;
        }

        var state = handle?.Value ?? SettingsOverrides.Empty;

        // Telegram resends an update that it thinks was lost: ours were already done, and a resent answer must not become a chat question.
        // Only ids the menu claimed are in the set, so an ordinary chat question is never mistaken for a replay.
        if (state.HandledUpdateIds.Contains(update.Id))
        {
            return true;
        }

        // An unreadable blob cannot record anything; the menu still has to open so that the owner can reset it.
        var canRecord = handle is not { Unreadable: true };
        return update.CallbackQuery is { } query
            ? await HandleCallbackAsync(update.Id, query, chat.Id, canRecord, ct)
            : await HandleMessageAsync(update, state, chat.Id, canRecord, ct);
    }

    private async Task<bool> HandleCallbackAsync(long updateId, CallbackQuery query, long chatId, bool canRecord, CancellationToken ct)
    {
        await panel.AnswerCallbackAsync(query.Id, null, ct);

        if (!await ClaimAsync(updateId, canRecord, ct) || query.Message is not { } message)
        {
            return true;
        }

        if (CallbackData.Decode(query.Data) is { } action)
        {
            await controller.HandleCallbackAsync(chatId, message.Id, action, ct);
        }
        else
        {
            log.LogInformation("A button with unknown data was pressed");
        }

        return true;
    }

    private async Task<bool> HandleMessageAsync(Update update, SettingsOverrides state, long chatId, bool canRecord, CancellationToken ct)
    {
        var text = update.Message?.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (text.TrimStart().StartsWith('/'))
        {
            if (BotCommandText.Parse(text)?.Name is "/settings" or "/cancel")
            {
                if (await ClaimAsync(update.Id, canRecord, ct))
                {
                    await controller.HandleCommandAsync(chatId, text, ct);
                }

                return true;
            }

            // Any other command abandons the question of this chat; the command itself is not ours.
            if (state.Pending is { } asked && asked.IsFor(chatId))
            {
                await menu.SetPendingAsync(null, ct);
            }

            return false;
        }

        // A question that another chat was asked is not ours to answer.
        if (state.Pending is not { } pending || !pending.IsFor(chatId))
        {
            return false;
        }

        // An abandoned question must not cost a blob write for every chat message from now on.
        if (pending.ExpiresAt <= time.GetUtcNow())
        {
            await menu.SetPendingAsync(null, ct);
            return false;
        }

        return await ClaimAsync(update.Id, canRecord, ct) && await controller.HandleTextAsync(chatId, text, ct);
    }

    private async Task<bool> ClaimAsync(long updateId, bool canRecord, CancellationToken ct) =>
        !canRecord || await menu.TryMarkHandledAsync(updateId, ct);
}
