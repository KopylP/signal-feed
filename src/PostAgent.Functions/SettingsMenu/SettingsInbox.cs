using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PostAgent.SettingsMenu;

// The first handler of the webhook: the settings menu, its buttons and the typed answer to its question. Everything else goes on to the chat.
public sealed class SettingsInbox(
    ChatResolver resolver,
    IOverridesStore store,
    SettingsEditor editor,
    MenuController controller,
    ITelegramPanel panel,
    ILogger<SettingsInbox> log) : IIncomingMessageHandler
{
    public async Task<bool> HandleAsync(Update update, CancellationToken ct)
    {
        var chat = update.CallbackQuery?.Message?.Chat ?? update.Message?.Chat;
        if (chat is null || await resolver.TryGetChatIdAsync(ct) is not { } bound)
        {
            return false;
        }

        // Only the bound private chat: a button press from anywhere else is swallowed, a message goes on and is ignored by the chat as well.
        if (chat.Type != ChatType.Private || chat.Id != bound)
        {
            return update.CallbackQuery is not null;
        }

        var state = (await store.LoadAsync(ct))?.Value ?? SettingsOverrides.Empty;

        // Telegram resends an update that it thinks was lost: ours were already done, and a resent answer must not become a chat question.
        if (update.Id <= state.LastUpdateId)
        {
            return true;
        }

        return update.CallbackQuery is { } query
            ? await HandleCallbackAsync(update.Id, query, chat.Id, ct)
            : await HandleMessageAsync(update, state, chat.Id, ct);
    }

    private async Task<bool> HandleCallbackAsync(long updateId, CallbackQuery query, long chatId, CancellationToken ct)
    {
        await panel.AnswerCallbackAsync(query.Id, null, ct);

        if (!await editor.TryAdvanceUpdateIdAsync(updateId, ct) || query.Message is not { } message)
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

    private async Task<bool> HandleMessageAsync(Update update, SettingsOverrides state, long chatId, CancellationToken ct)
    {
        var text = update.Message?.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (text.TrimStart().StartsWith('/'))
        {
            if (MenuController.CommandName(text) is "/settings" or "/cancel")
            {
                if (await editor.TryAdvanceUpdateIdAsync(update.Id, ct))
                {
                    await controller.HandleCommandAsync(chatId, text, ct);
                }

                return true;
            }

            // Any other command abandons the question; the command itself is not ours.
            if (state.Pending is not null)
            {
                await editor.SetPendingAsync(null, ct);
            }

            return false;
        }

        if (state.Pending is null || !await editor.TryAdvanceUpdateIdAsync(update.Id, ct))
        {
            return false;
        }

        return await controller.HandleTextAsync(chatId, text, ct);
    }
}
