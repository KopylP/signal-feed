using System.Runtime.CompilerServices;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;
using TL;
using WTelegram;

namespace PostAgent.Collecting.Infrastructure.Telegram;

public sealed class TelegramSourceReader(
    IOptions<AgentOptions> options,
    BlobServiceClient blobs,
    TimeProvider time) : ISourceReader
{
    private const int PageSize = 100;
    private const int FloodRetryThresholdSeconds = 60;

    private Client? _client;
    private BlobSessionStream? _session;

    public async Task<SourceBatch> ReadAsync(string username, int lastPostId, CancellationToken ct)
    {
        try
        {
            return await ReadSourceAsync(username, lastPostId, ct);
        }
        catch (RpcException ex) when (ex.Message.StartsWith("FLOOD_WAIT", StringComparison.Ordinal))
        {
            throw new SourceThrottledException(ex.Message, ex);
        }
    }

    private async Task<SourceBatch> ReadSourceAsync(string username, int lastPostId, CancellationToken ct)
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
            messages.AddRange(page.Messages.Select(m => TgMessageMapper.Map(m, page, type)));
            if (cutoff is not null && OldestDate(page) < cutoff)
            {
                break;
            }
        }

        return cutoff is null ? messages : messages.Where(m => m.Date.UtcDateTime >= cutoff).ToList();
    }

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

    private static DateTime OldestDate(Messages_MessagesBase page) =>
        page.Messages.Where(m => m.Date != default).Select(m => m.Date).DefaultIfEmpty(DateTime.MaxValue).Min();

    private async Task<Client> ConnectAsync(CancellationToken ct)
    {
        var container = blobs.GetBlobContainerClient(options.Value.SessionBlobContainer);
        _session = await BlobSessionStream.LoadAsync(container, ct);

        var client = new Client(ConfigValue, _session) { FloodRetryThreshold = FloodRetryThresholdSeconds };
        await client.LoginUserIfNeeded();
        return client;
    }

    private string? ConfigValue(string what) => what switch
    {
        "api_id" => options.Value.TelegramApiId.ToString(),
        "api_hash" => options.Value.TelegramApiHash,
        // Whoever signed in owns the session: without this WTelegram compares the account's phone with "phone_number"
        // and logs the session out on a mismatch, which would throw away a sign-in made with another number.
        "user_id" => "-1",
        // Asked only when the session is no longer signed in. The collector never asks Telegram for a code itself.
        "phone_number" or "verification_code" or "password" => throw new InvalidOperationException("Telegram session is expired. Send /login to the bot (or run TgLogin)."),
        _ => null,
    };

    public void Dispose()
    {
        _client?.Dispose();
        _session?.Dispose();
    }
}
