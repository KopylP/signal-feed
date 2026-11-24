using System.Globalization;
using Microsoft.Extensions.Options;
using PostAgent.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PostAgent.Services;

/// <summary>Надсилає повідомлення власнику через Telegram Bot API (бібліотека Telegram.Bot).</summary>
public sealed class TelegramNotifier(HttpClient http, IOptions<AgentOptions> options)
{
    private readonly TelegramBotClient _bot = new(options.Value.TelegramBotToken, http);
    private readonly ChatId _chat = new(long.Parse(options.Value.TelegramChatId, CultureInfo.InvariantCulture));

    public Task SendDigestAsync(DigestReport report, CancellationToken ct) =>
        SendAsync(TelegramMessages.FormatDigest(report), ct);

    public Task SendNoPostsAsync(CancellationToken ct) => SendAsync(TelegramMessages.NoPosts, ct);

    public Task SendAlertAsync(IReadOnlyList<PostEntity> posts, CancellationToken ct) =>
        SendAsync(TelegramMessages.FormatAlert(posts), ct);

    public Task SendHealthAsync(IReadOnlyList<string> failures, CancellationToken ct) =>
        SendAsync(TelegramMessages.FormatHealth(failures), ct);

    private Task SendAsync(string html, CancellationToken ct) =>
        _bot.SendMessage(
            _chat,
            html,
            ParseMode.Html,
            linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
            cancellationToken: ct);
}
