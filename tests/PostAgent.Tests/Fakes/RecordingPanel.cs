using PostAgent.Shared.Notifications;

namespace PostAgent.Tests.Fakes;

internal sealed class RecordingPanel : ITelegramPanel
{
    private int _nextId = 100;

    public List<(long ChatId, int MessageId, string Html, InlineKeyboard Keyboard)> Sent { get; } = [];

    public List<(long ChatId, int MessageId, string Html, InlineKeyboard Keyboard)> Edited { get; } = [];

    public List<(string Id, string? Text)> Answered { get; } = [];

    // Makes EditAsync throw, like Telegram does for a message that is gone or older than 48 hours.
    public bool FailEdits { get; set; }

    // Makes AnswerCallbackAsync throw, like Telegram does for a query that is too old.
    public bool FailAnswers { get; set; }

    public Task<int> SendAsync(long chatId, string html, InlineKeyboard keyboard, CancellationToken ct)
    {
        var id = _nextId++;
        Sent.Add((chatId, id, html, keyboard));
        return Task.FromResult(id);
    }

    public Task EditAsync(long chatId, int messageId, string html, InlineKeyboard keyboard, CancellationToken ct)
    {
        if (FailEdits)
        {
            throw new InvalidOperationException("message to edit not found");
        }

        Edited.Add((chatId, messageId, html, keyboard));
        return Task.CompletedTask;
    }

    public Task AnswerCallbackAsync(string callbackQueryId, string? text, CancellationToken ct)
    {
        if (FailAnswers)
        {
            throw new InvalidOperationException("query is too old");
        }

        Answered.Add((callbackQueryId, text));
        return Task.CompletedTask;
    }
}
