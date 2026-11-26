using Azure;
using Azure.Data.Tables;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Shared.Notifications.Telegram;

// One row per chat: partition "chat", row key = the chat id. The single row of the earlier version (row key "digest") still counts as a chat.
public sealed class ChatBindingStore(TableServiceClient tables) : IChatBindingStore
{
    private const string Partition = "chat";

    private readonly TableClient _table = tables.GetTableClient(StorageNames.ChatBindingTable);

    public async Task<IReadOnlyList<long>> GetAllAsync(CancellationToken ct)
    {
        var rows = new List<(DateTimeOffset At, long ChatId)>();
        await foreach (var row in _table.QueryAsync<TableEntity>(e => e.PartitionKey == Partition, cancellationToken: ct))
        {
            if (row.TryGetValue("ChatId", out var value) && value is long chatId)
            {
                rows.Add((row.Timestamp ?? DateTimeOffset.MinValue, chatId));
            }
        }

        return rows.OrderBy(r => r.At).Select(r => r.ChatId).Distinct().ToList();
    }

    public async Task<BindResult> TryBindAsync(long chatId, int max, CancellationToken ct)
    {
        var bound = await GetAllAsync(ct);
        if (bound.Contains(chatId))
        {
            return BindResult.AlreadyBound;
        }

        if (bound.Count >= max)
        {
            return BindResult.Full;
        }

        try
        {
            // Insert-only: the same chat paired twice at once is added once. Two different chats racing for the last place can both get in,
            // which at worst is one chat over the limit.
            await _table.AddEntityAsync(
                new TableEntity(Partition, chatId.ToString(System.Globalization.CultureInfo.InvariantCulture)) { ["ChatId"] = chatId }, ct);
            return BindResult.Added;
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            return BindResult.AlreadyBound;
        }
    }
}
