using Microsoft.Extensions.Options;
using PostAgent.Shared.Settings;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PostAgent.Shared.Notifications.Telegram;

public sealed class TelegramNotifier(HttpClient http, IOptions<AgentOptions> options, ChatResolver chat) : ITelegramNotifier
{
    private readonly TelegramBotClient _bot = new(options.Value.TelegramBotToken, http);

    public async Task SendAsync(string html, CancellationToken ct) =>
        await _bot.SendMessage(
            new ChatId(await chat.GetChatIdAsync(ct)),
            html,
            ParseMode.Html,
            linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
            cancellationToken: ct);
}
