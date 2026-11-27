using PostAgent.Shared.Chat;
using PostAgent.Shared.Notifications;
using Telegram.Bot.Types;

namespace PostAgent.Chat.Intake;

// /clear: the reader forgets the conversation, typed by them, so it needs no confirmation (nor does the agent's clear_context, ADR-0021).
public sealed class ClearInbox(
    ChatResolver resolver,
    IChatStateStore state,
    ITelegramNotifier notifier,
    ILogger<ClearInbox> log) : IIncomingMessageHandler
{
    public async Task<bool> HandleAsync(Update update, CancellationToken ct)
    {
        if (update.Message is not { Text: { } text } message || BotCommandText.Parse(text)?.Name != "/clear")
        {
            return false;
        }

        if (!await resolver.ServesAsync(message.Chat, ct))
        {
            return false;
        }

        if (!await state.ClearConversationAsync(message.Chat.Id, ct))
        {
            log.LogWarning("The conversation could not be cleared, it kept changing");
            await notifier.SendToAsync(message.Chat.Id, ChatTexts.Failed, ct);
            return true;
        }

        await notifier.SendToAsync(message.Chat.Id, ChatTexts.Cleared, ct);
        return true;
    }
}
