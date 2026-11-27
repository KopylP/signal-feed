using PostAgent.Shared.Chat;
using PostAgent.Shared.Common;
using PostAgent.Shared.Notifications;
using Telegram.Bot.Types;

namespace PostAgent.Chat.Intake;

// The webhook must answer Telegram fast, so a question is only checked and queued here; the answer is made by ChatProcessor.
public sealed class ChatInbox(
    ChatResolver resolver,
    IChatStateStore state,
    IChatQueue queue,
    ITelegramNotifier notifier,
    TimeProvider time,
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
        if (!await resolver.ServesAsync(message.Chat, ct))
        {
            return false;
        }

        var current = await StateAsync(message.Chat.Id, ct);
        if (current is null)
        {
            await notifier.SendToAsync(message.Chat.Id, ChatTexts.Failed, ct);
            return true;
        }

        try
        {
            await queue.EnqueueAsync(new ChatRequest(update.Id, QuestionText.From(message, text.Trim().Truncate(MaxQuestionChars)), current.State.Report.ReportId, message.Chat.Id, SentAt(message)), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Telegram would resend the update on a 5xx, but only for a while; the reader can just ask again.
            log.LogError(ex, "A chat question could not be queued");
        }

        return true;
    }

    // Telegram dates are UTC; a missing one (default) is not sent as metadata.
    private static DateTimeOffset? SentAt(Message message) =>
        message.Date == default ? null : new DateTimeOffset(DateTime.SpecifyKind(message.Date, DateTimeKind.Utc));

    // Before the first digest there is no conversation yet: it starts empty, and the digest replaces it when it comes.
    private async Task<ChatStateHandle?> StateAsync(long chatId, CancellationToken ct)
    {
        var current = await state.LoadAsync(chatId, ct);
        if (current is not null)
        {
            return current;
        }

        await state.TryStartAsync(chatId, ReportSnapshot.None(time.GetUtcNow()), ct);
        return await state.LoadAsync(chatId, ct);
    }
}
