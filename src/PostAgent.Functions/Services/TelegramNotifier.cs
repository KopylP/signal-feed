using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using PostAgent.Models;

namespace PostAgent.Services;

/// <summary>Надсилає повідомлення власнику через Telegram Bot API.</summary>
public sealed class TelegramNotifier(IHttpClientFactory httpClients, IOptions<AgentOptions> options)
{
    public const string HttpClientName = "telegram-bot";

    public Task SendDigestAsync(IReadOnlyList<DigestItem> items, CancellationToken ct) =>
        SendAsync(TelegramMessages.FormatDigest(items), ct);

    public Task SendNoPostsAsync(CancellationToken ct) => SendAsync(TelegramMessages.NoPosts, ct);

    public Task SendAlertAsync(IReadOnlyList<PostEntity> posts, CancellationToken ct) =>
        SendAsync(TelegramMessages.FormatAlert(posts), ct);

    private async Task SendAsync(string html, CancellationToken ct)
    {
        var settings = options.Value;
        var http = httpClients.CreateClient(HttpClientName);

        using var response = await http.PostAsJsonAsync(
            $"bot{settings.TelegramBotToken}/sendMessage",
            new
            {
                chat_id = settings.TelegramChatId,
                text = html,
                parse_mode = "HTML",
                link_preview_options = new { is_disabled = true },
            },
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Telegram sendMessage failed: {(int)response.StatusCode} {body}");
        }
    }
}
