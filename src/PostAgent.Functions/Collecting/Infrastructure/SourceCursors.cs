using Azure.Data.Tables;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Collecting.Infrastructure;

public sealed class SourceCursors(TableServiceClient tables) : ISourceCursors
{
    private const string Partition = "source";

    private readonly TableClient _table = tables.GetTableClient(StorageNames.CursorsTable);

    public async Task<int> GetLastPostIdAsync(string username, CancellationToken ct)
    {
        var cursor = await _table.GetEntityIfExistsAsync<TableEntity>(Partition, username, cancellationToken: ct);
        // On a missing row, Value throws instead of returning null, so check HasValue first.
        return cursor.HasValue ? cursor.Value?.GetInt32("LastPostId") ?? 0 : 0;
    }

    public Task SetLastPostIdAsync(string username, int lastPostId, CancellationToken ct) =>
        _table.UpsertEntityAsync(
            new TableEntity(Partition, username) { ["LastPostId"] = lastPostId },
            TableUpdateMode.Replace,
            ct);

    public async Task DeleteExceptAsync(IReadOnlyCollection<string> keep, CancellationToken ct)
    {
        var stale = new List<string>();
        await foreach (var row in _table.QueryAsync<TableEntity>($"PartitionKey eq '{Partition}'", select: ["RowKey"], cancellationToken: ct))
        {
            if (!keep.Contains(row.RowKey))
            {
                stale.Add(row.RowKey);
            }
        }

        foreach (var username in stale)
        {
            await _table.DeleteEntityAsync(Partition, username, cancellationToken: ct);
        }
    }
}
