using Azure.Data.Tables;

namespace PostAgent.Storage;

/// <summary>Курсор читання кожного джерела: id останнього прочитаного повідомлення (таблиця ChannelState).</summary>
public sealed class SourceCursors(TableServiceClient tables)
{
    public const string TableName = "ChannelState";

    private const string Partition = "source";

    private readonly TableClient _table = tables.GetTableClient(TableName);

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
