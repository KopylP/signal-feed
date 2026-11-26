namespace PostAgent.Shared.Notifications;

// What an interactive message needs beyond ITelegramNotifier: a message with buttons that is edited in place, and the answer to a button press.
public interface ITelegramPanel
{
    // Returns the id of the new message, so that it can be edited later.
    Task<int> SendAsync(long chatId, string html, InlineKeyboard keyboard, CancellationToken ct);

    // "Message is not modified" is not an error: a second press of the same button shows the same screen.
    Task EditAsync(long chatId, int messageId, string html, InlineKeyboard keyboard, CancellationToken ct);

    // Stops the spinner on the pressed button; a query that is too old is not an error.
    Task AnswerCallbackAsync(string callbackQueryId, string? text, CancellationToken ct);
}
