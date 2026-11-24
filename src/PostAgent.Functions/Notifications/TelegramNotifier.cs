using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using PostAgent.Settings;

namespace PostAgent.Notifications;

public sealed class TelegramNotifier(HttpClient http, IOptions<AgentOptions> options) : ITelegramNotifier
{
    private readonly TelegramBotClient _bot = new(options.Value.TelegramBotToken, http);
    private readonly ChatId _chat = new(options.Value.TelegramChatId);

    public Task SendAsync(string html, CancellationToken ct) =>
        _bot.SendMessage(
            _chat,
            html,
            ParseMode.Html,
            linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
            cancellationToken: ct);
}
