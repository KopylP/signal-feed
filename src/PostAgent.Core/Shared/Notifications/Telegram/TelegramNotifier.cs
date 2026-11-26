using Microsoft.Extensions.Options;
using PostAgent.Shared.Settings;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace PostAgent.Shared.Notifications.Telegram;

public sealed class TelegramNotifier(
    HttpClient http,
    IOptions<AgentOptions> options,
    ChatResolver chat,
    TimeProvider time,
    ILogger<TelegramNotifier> log) : ITelegramNotifier
{
    private readonly TelegramBotClient _bot = new(options.Value.TelegramBotToken, http);

    public Task SendAsync(string html, CancellationToken ct) => SendAsync(html, InlineKeyboard.Empty, ct);

    public async Task SendAsync(string html, InlineKeyboard keyboard, CancellationToken ct)
    {
        var chats = await chat.RequireChatIdsAsync(ct);
        var failures = new List<Exception>();
        foreach (var chatId in chats)
        {
            try
            {
                await SendOneAsync(chatId, html, keyboard, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One chat that cannot be reached (the bot was blocked) must not keep the others from their message.
                failures.Add(ex);
            }
        }

        if (failures.Count == chats.Count)
        {
            throw failures.Count == 1 ? failures[0] : new AggregateException("The message reached no chat", failures);
        }
    }

    public Task SendToAsync(long chatId, string html, CancellationToken ct) => SendOneAsync(chatId, html, InlineKeyboard.Empty, ct);

    public Task SendToAsync(long chatId, string html, ReplyKeyboard keyboard, CancellationToken ct) =>
        SendOneAsync(chatId, html, TelegramKeyboard.ToMarkup(keyboard), ct);

    private Task SendOneAsync(long chatId, string html, InlineKeyboard keyboard, CancellationToken ct) =>
        SendOneAsync(chatId, html, TelegramKeyboard.ToMarkup(keyboard), ct);

    private async Task SendOneAsync(long chatId, string html, ReplyMarkup? markup, CancellationToken ct)
    {
        var started = time.GetTimestamp();
        try
        {
            await _bot.SendMessage(
                new ChatId(chatId),
                html,
                ParseMode.Html,
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                replyMarkup: markup,
                cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The length and the time only: the text is never logged. They show whether slow sends depend on the size.
            log.LogWarning("Telegram send failed: {Chars} chars after {Ms} ms", html.Length, (long)time.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }

        log.LogInformation("Telegram message sent: {Chars} chars in {Ms} ms", html.Length, (long)time.GetElapsedTime(started).TotalMilliseconds);
    }
}
