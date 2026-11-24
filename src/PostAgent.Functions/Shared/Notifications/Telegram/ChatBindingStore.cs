using Azure;
using Azure.Data.Tables;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Shared.Notifications.Telegram;

public sealed class ChatBindingStore(TableServiceClient tables) : IChatBindingStore
{
    private const string Partition = "chat";
    private const string Row = "digest";

    private readonly TableClient _table = tables.GetTableClient(StorageNames.ChatBindingTable);

    public async Task<long?> GetAsync(CancellationToken ct)
    {
        var binding = await _table.GetEntityIfExistsAsync<TableEntity>(Partition, Row, cancellationToken: ct);
        return binding.HasValue ? binding.Value!.GetInt64("ChatId") : null;
    }

    public async Task<bool> TryBindAsync(long chatId, CancellationToken ct)
    {
        try
        {
            // Insert-only: two simultaneous pairings cannot both succeed.
            await _table.AddEntityAsync(new TableEntity(Partition, Row) { ["ChatId"] = chatId }, ct);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            return false;
        }
    }
}
