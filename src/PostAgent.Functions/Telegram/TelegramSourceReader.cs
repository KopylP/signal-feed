using System.Runtime.CompilerServices;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using PostAgent.Models;
using TL;
using WTelegram;

namespace PostAgent.Telegram;

/// <summary>
/// Читає канали й групи через MTProto як звичайний користувач.
/// FLOOD_WAIT до FloodRetryThresholdSeconds бібліотека чекає сама, довший кидає RpcException.
/// </summary>
public sealed class TelegramSourceReader(
    IOptions<AgentOptions> options,
    BlobServiceClient blobs,
    TimeProvider time,
    ILogger<TelegramSourceReader> log) : IDisposable
{
    private const int PageSize = 100;
    private const int FloodRetryThresholdSeconds = 60;

    private Client? _client;
    private BlobSessionStream? _session;

    public async Task<SourceBatch> ReadAsync(string username, int lastPostId, CancellationToken ct)
    {
        var client = _client ??= await ConnectAsync(ct);
        var channel = await ResolveChannelAsync(client, username);

        var source = new SourceInfo(username, channel.IsGroup ? SourceType.Group : SourceType.Channel);
        var messages = await ReadNewMessagesAsync(client, channel, source.Type, lastPostId, ct);
        return new SourceBatch(source, messages, messages.Count == 0 ? lastPostId : messages.Max(m => m.Id));
    }

    private static async Task<Channel> ResolveChannelAsync(Client client, string username)
    {
        var resolved = await client.Contacts_ResolveUsername(username);
        return resolved.Chat as Channel
               ?? throw new InvalidOperationException($"@{username} is not a channel or a public group.");
    }

    private async Task<List<TgMessage>> ReadNewMessagesAsync(
        Client client, Channel channel, SourceType type, int lastPostId, CancellationToken ct)
    {
        // Перший запуск: min_id = 0 означав би всю історію, тому беремо лише останні години.
        DateTime? cutoff = lastPostId == 0
            ? time.GetUtcNow().UtcDateTime.AddHours(-options.Value.FirstRunLookbackHours)
            : null;

        var messages = new List<TgMessage>();
        await foreach (var page in ReadPagesAsync(client, channel, lastPostId, ct))
        {
            messages.AddRange(page.Messages.Select(m => ToTgMessage(m, page, type)));
            if (cutoff is not null && OldestDate(page) < cutoff)
            {
                break;
            }
        }

        return cutoff is null ? messages : messages.Where(m => m.Date.UtcDateTime >= cutoff).ToList();
    }

    /// <summary>Сторінки історії від новіших до старіших, доки не закінчаться нові повідомлення.</summary>
    private static async IAsyncEnumerable<Messages_MessagesBase> ReadPagesAsync(
        Client client, Channel channel, int minId, [EnumeratorCancellation] CancellationToken ct)
    {
        var offsetId = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var page = await client.Messages_GetHistory(channel, offset_id: offsetId, limit: PageSize, min_id: minId);
            if (page.Messages is not { Length: > 0 } messages)
            {
                yield break;
            }

            yield return page;
            offsetId = messages.Min(m => m.ID);
        }
    }

    /// <summary>MessageEmpty не має дати (default), тому враховуємо лише справжні повідомлення.</summary>
    private static DateTime OldestDate(Messages_MessagesBase page) =>
        page.Messages.Where(m => m.Date != default).Select(m => m.Date).DefaultIfEmpty(DateTime.MaxValue).Min();

    private static TgMessage ToTgMessage(MessageBase message, Messages_MessagesBase page, SourceType type)
    {
        var date = new DateTimeOffset(DateTime.SpecifyKind(message.Date, DateTimeKind.Utc));
        if (message is not Message m)
        {
            return new TgMessage(message.ID, date, null, null, 0, IsService: true);
        }

        var author = type == SourceType.Group ? AuthorName(page, m) : null;
        return new TgMessage(m.id, date, m.message, author, Engagement(m), IsService: false);
    }

    /// <summary>Реакції + 2 × пересилання + відповіді: чим більше, тим помітніше повідомлення.</summary>
    private static int Engagement(Message m) =>
        (m.reactions?.results?.Sum(r => r.count) ?? 0) + 2 * m.forwards + (m.replies?.replies ?? 0);

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
        Helpers.Log = (level, text) => log.Log((LogLevel)level, "{Text}", text);

        var container = blobs.GetBlobContainerClient(options.Value.SessionBlobContainer);
        _session = await BlobSessionStream.LoadAsync(container, ct);

        var client = new Client(ConfigValue, _session) { FloodRetryThreshold = FloodRetryThresholdSeconds };
        await client.LoginUserIfNeeded();
        return client;
    }

    /// <summary>Значення, яких просить WTelegramClient. Код і пароль без людини недоступні: потрібен TgLogin.</summary>
    private string? ConfigValue(string what) => what switch
    {
        "api_id" => options.Value.TelegramApiId.ToString(),
        "api_hash" => options.Value.TelegramApiHash,
        "phone_number" => options.Value.TelegramPhone,
        "verification_code" or "password" => throw new InvalidOperationException("Telegram session is expired. Run TgLogin again."),
        _ => null,
    };

    public void Dispose()
    {
        _client?.Dispose();
        _session?.Dispose();
    }
}
