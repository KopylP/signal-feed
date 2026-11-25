using Microsoft.Extensions.Options;
using PostAgent.Shared.Settings;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

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
        var started = time.GetTimestamp();
        try
        {
            await _bot.SendMessage(
                new ChatId(await chat.GetChatIdAsync(ct)),
                html,
                ParseMode.Html,
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                replyMarkup: TelegramKeyboard.ToMarkup(keyboard),
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
