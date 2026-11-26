using PostAgent.Shared.Chat;
using PostAgent.Shared.Notifications;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PostAgent.Chat.Intake;

// /clear: the reader forgets the conversation, typed by them, so it needs no confirmation (the agent's own proposal to clear does, see ProposeClearContextTool).
public sealed class ClearInbox(
    ChatResolver resolver,
    IChatStateStore state,
    ITelegramNotifier notifier,
    ILogger<ClearInbox> log) : IIncomingMessageHandler
{
    public async Task<bool> HandleAsync(Update update, CancellationToken ct)
    {
        if (update.Message is not { Text: { } text } message || CommandName(text) != "/clear")
        {
            return false;
        }

        if (message.Chat.Type != ChatType.Private || !await resolver.IsBoundAsync(message.Chat.Id, ct))
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

    private static string CommandName(string text) => text.Trim().Split(' ', 2)[0].Split('@', 2)[0].ToLowerInvariant();
}
