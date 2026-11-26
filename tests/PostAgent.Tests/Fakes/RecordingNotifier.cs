using PostAgent.Shared.Notifications;

namespace PostAgent.Tests.Fakes;

internal sealed class RecordingNotifier : ITelegramNotifier
{
    public List<string> Sent { get; } = [];

    // The keyboard of the message at the same index in Sent (Empty for a plain message).
    public List<InlineKeyboard> Keyboards { get; } = [];

    // The 1-based number of the call that fails (0: none), to test what happens when a later message cannot be sent.
    public int FailOnCall { get; init; }

    // Makes every message to all chats fail, while a message to one chat still goes through.
    public bool FailBroadcasts { get; init; }

    // Messages sent to one chat, in order (the answers and the pairing replies).
    public List<(long ChatId, string Html)> SentTo { get; } = [];

    // The texts of SentTo, for tests that do not care which chat got them.
    public List<string> Replies => SentTo.Select(m => m.Html).ToList();

    // The reply keyboard of the message at the same index in SentTo (null for a message sent without one).
    public List<ReplyKeyboard?> ReplyKeyboards { get; } = [];

    public Task SendToAsync(long chatId, string html, CancellationToken ct) => SendTo(chatId, html, null);

    public Task SendToAsync(long chatId, string html, ReplyKeyboard keyboard, CancellationToken ct) => SendTo(chatId, html, keyboard);

    private Task SendTo(long chatId, string html, ReplyKeyboard? keyboard)
    {
        if (Sent.Count + SentTo.Count + 1 == FailOnCall)
        {
            throw new InvalidOperationException("Telegram is down");
        }

        SentTo.Add((chatId, html));
        ReplyKeyboards.Add(keyboard);
        return Task.CompletedTask;
    }

    public Task SendAsync(string html, CancellationToken ct) => SendAsync(html, InlineKeyboard.Empty, ct);

    public Task SendAsync(string html, InlineKeyboard keyboard, CancellationToken ct)
    {
        if (FailBroadcasts || Sent.Count + SentTo.Count + 1 == FailOnCall)
        {
            throw new InvalidOperationException("Telegram is down");
        }

        Sent.Add(html);
        Keyboards.Add(keyboard);
        return Task.CompletedTask;
    }
}
