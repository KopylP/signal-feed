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
        return cursor.HasValue ? cursor.Value!.GetInt32("LastPostId") ?? 0 : 0;
    }

    public Task SetLastPostIdAsync(string username, int lastPostId, CancellationToken ct) =>
        _table.UpsertEntityAsync(
            new TableEntity(Partition, username) { ["LastPostId"] = lastPostId },
            TableUpdateMode.Replace,
            ct);
}
