using Microsoft.Extensions.Options;
using PostAgent.Shared.Settings;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PostAgent.Shared.Notifications.Telegram;

public sealed class TelegramPanel(HttpClient http, IOptions<AgentOptions> options, ILogger<TelegramPanel> log) : ITelegramPanel
{
    private static readonly LinkPreviewOptions NoPreview = new() { IsDisabled = true };

    private readonly TelegramBotClient _bot = new(options.Value.TelegramBotToken, http);

    public async Task<int> SendAsync(long chatId, string html, InlineKeyboard keyboard, CancellationToken ct)
    {
        var message = await _bot.SendMessage(
            new ChatId(chatId), html, ParseMode.Html, linkPreviewOptions: NoPreview, replyMarkup: TelegramKeyboard.ToMarkup(keyboard), cancellationToken: ct);

        // The length only: the text is never logged.
        log.LogInformation("Telegram panel message sent: {Chars} chars", html.Length);
        return message.Id;
    }

    public async Task EditAsync(long chatId, int messageId, string html, InlineKeyboard keyboard, CancellationToken ct)
    {
        try
        {
            await _bot.EditMessageText(
                new ChatId(chatId), messageId, html, ParseMode.Html, linkPreviewOptions: NoPreview, replyMarkup: TelegramKeyboard.ToMarkup(keyboard), cancellationToken: ct);
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("message is not modified", StringComparison.OrdinalIgnoreCase))
        {
            log.LogDebug("Telegram panel edit skipped: the message is not modified");
        }
    }

    public async Task EditKeyboardAsync(long chatId, int messageId, InlineKeyboard keyboard, CancellationToken ct)
    {
        try
        {
            await _bot.EditMessageReplyMarkup(new ChatId(chatId), messageId, TelegramKeyboard.ToMarkup(keyboard), cancellationToken: ct);
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("message is not modified", StringComparison.OrdinalIgnoreCase))
        {
            log.LogDebug("Telegram keyboard edit skipped: the message is not modified");
        }
    }

    public async Task AnswerCallbackAsync(string callbackQueryId, string? text, CancellationToken ct)
    {
        try
        {
            await _bot.AnswerCallbackQuery(callbackQueryId, text, cancellationToken: ct);
        }
        catch (ApiRequestException ex)
        {
            // A query is valid for about a minute; the press itself was already handled.
            log.LogInformation("Telegram callback answer failed: {Code}", ex.ErrorCode);
        }
    }
}
