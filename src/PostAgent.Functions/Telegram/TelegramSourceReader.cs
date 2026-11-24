using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using PostAgent.Models;
using TL;
using WTelegram;

namespace PostAgent.Telegram;

public sealed record SourceBatch(SourceInfo Source, IReadOnlyList<TgMessage> Messages, int LastPostId);

/// <summary>
/// Читає канали й групи через MTProto як звичайний користувач.
/// FLOOD_WAIT до 60 с бібліотека чекає сама (FloodRetryThreshold), довший кидає RpcException.
/// </summary>
public sealed class TelegramSourceReader(
    IOptions<AgentOptions> options,
    BlobServiceClient blobs,
    TimeProvider time,
    ILogger<TelegramSourceReader> log) : IAsyncDisposable
{
    private const int PageSize = 100;

    private Client? _client;
    private BlobSessionStream? _session;

    public async Task<SourceBatch> ReadAsync(string username, int lastPostId, CancellationToken ct)
    {
        var client = _client ??= await ConnectAsync(ct);

        var resolved = await client.Contacts_ResolveUsername(username);
        if (resolved.Chat is not Channel channel)
        {
            throw new InvalidOperationException($"@{username} is not a channel or a public group.");
        }

        var source = new SourceInfo(username, channel.IsGroup ? SourceType.Group : SourceType.Channel);
        var messages = await ReadNewMessagesAsync(client, channel, source, lastPostId, ct);
        return new SourceBatch(source, messages, messages.Count == 0 ? lastPostId : messages.Max(m => m.Id));
    }

    private async Task<List<TgMessage>> ReadNewMessagesAsync(
        Client client, Channel channel, SourceInfo source, int lastPostId, CancellationToken ct)
    {
        // Перший запуск: min_id = 0 означав би всю історію, тому беремо лише останні години.
        DateTimeOffset? cutoff = lastPostId == 0
            ? time.GetUtcNow().AddHours(-options.Value.FirstRunLookbackHours)
            : null;

        var result = new List<TgMessage>();
        var offsetId = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var page = await client.Messages_GetHistory(channel, offset_id: offsetId, limit: PageSize, min_id: lastPostId);
            var batch = page.Messages ?? [];
            if (batch.Length == 0)
            {
                break;
            }

            result.AddRange(batch.Select(m => Convert(m, page, source.Type)));
            offsetId = batch.Min(m => m.ID);

            if (cutoff is not null && batch.Min(m => m.Date) < cutoff.Value.UtcDateTime)
            {
                break;
            }
        }

        return cutoff is null ? result : result.Where(m => m.Date >= cutoff).ToList();
    }

    private static TgMessage Convert(MessageBase message, Messages_MessagesBase page, SourceType type)
    {
        var date = new DateTimeOffset(DateTime.SpecifyKind(message.Date, DateTimeKind.Utc));

        if (message is not Message m)
        {
            return new TgMessage(message.ID, date, null, null, 0, IsService: true);
        }

        var engagement = (m.reactions?.results?.Sum(r => r.count) ?? 0) + 2 * m.forwards + (m.replies?.replies ?? 0);
        var author = type == SourceType.Group ? AuthorName(page, m) : null;
        return new TgMessage(m.id, date, m.message, author, engagement, IsService: false);
    }

    private static string? AuthorName(Messages_MessagesBase page, Message m)
    {
        if (m.from_id is null || page.UserOrChat(m.from_id) is not User user)
        {
            return null;
        }
        return user.MainUsername ?? $"{user.first_name} {user.last_name}".Trim();
    }

    private async Task<Client> ConnectAsync(CancellationToken ct)
    {
        var settings = options.Value;
        Helpers.Log = (level, text) => log.Log((LogLevel)level, "{Text}", text);

        var container = blobs.GetBlobContainerClient(settings.SessionBlobContainer);
        _session = await BlobSessionStream.LoadAsync(container, ct);

        var client = new Client(what => what switch
        {
            "api_id" => settings.TelegramApiId.ToString(),
            "api_hash" => settings.TelegramApiHash,
            "phone_number" => settings.TelegramPhone,
            "verification_code" or "password" =>
                throw new InvalidOperationException("Telegram session is expired. Run TgLogin again."),
            _ => null,
        }, _session)
        {
            FloodRetryThreshold = 60,
        };

        await client.LoginUserIfNeeded();
        return client;
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _session?.Dispose();
        return ValueTask.CompletedTask;
    }
}
