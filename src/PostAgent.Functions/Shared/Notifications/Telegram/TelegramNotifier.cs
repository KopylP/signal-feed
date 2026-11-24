using Microsoft.Extensions.Options;
using PostAgent.Shared.Settings;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PostAgent.Shared.Notifications.Telegram;

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
