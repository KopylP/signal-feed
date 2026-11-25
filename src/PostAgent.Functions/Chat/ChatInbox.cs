using PostAgent.Shared.Chat;
using PostAgent.Shared.Common;
using PostAgent.Shared.Notifications;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PostAgent.Chat;

// The webhook must answer Telegram fast, so a question is only checked and queued here; the answer is made by ChatProcessor.
public sealed class ChatInbox(
    ChatResolver resolver,
    IChatStateStore state,
    IChatQueue queue,
    ITelegramNotifier notifier,
    ILogger<ChatInbox> log) : IIncomingMessageHandler
{
    private const int MaxQuestionChars = 2000;

    public async Task<bool> HandleAsync(Update update, CancellationToken ct)
    {
        // A message that starts with "/" is a bot command (pairing's /start, for one), not a question.
        if (update.Message is not { Text: { } text } message || string.IsNullOrWhiteSpace(text) || text.TrimStart().StartsWith('/'))
        {
            return false;
        }

        // Only the private chat with the bot is served: in a group every member's message would cost a model call.
        if (message.Chat.Type != ChatType.Private || !await resolver.IsBoundAsync(message.Chat.Id, ct))
        {
            return false;
        }

        var current = await state.LoadAsync(message.Chat.Id, ct);
        if (current is null)
        {
            await notifier.SendToAsync(message.Chat.Id, ChatTexts.NoReport, ct);
            return true;
        }

        try
        {
            await queue.EnqueueAsync(new ChatRequest(update.Id, QuestionText.From(message, text.Trim().Truncate(MaxQuestionChars)), current.State.Report.ReportId, message.Chat.Id), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Telegram would resend the update on a 5xx, but only for a while; the reader can just ask again.
            log.LogError(ex, "A chat question could not be queued");
        }

        return true;
    }
}
