using Azure;
using Azure.Data.Tables;
using PostAgent.Models;

namespace PostAgent.Services;

/// <summary>Таблиці ChannelState і Posts.</summary>
public sealed class PostRepository(TableServiceClient tables)
{
    public const string PostsTable = "Posts";
    public const string ChannelStateTable = "ChannelState";
    private const string StatePartition = "source";

    private readonly TableClient _posts = tables.GetTableClient(PostsTable);
    private readonly TableClient _state = tables.GetTableClient(ChannelStateTable);

    public async Task<int> GetLastPostIdAsync(string username, CancellationToken ct)
    {
        var result = await _state.GetEntityIfExistsAsync<TableEntity>(StatePartition, username, cancellationToken: ct);
        return result.HasValue ? result.Value!.GetInt32("LastPostId") ?? 0 : 0;
    }

    public Task SetLastPostIdAsync(string username, int lastPostId, CancellationToken ct) =>
        _state.UpsertEntityAsync(
            new TableEntity(StatePartition, username) { ["LastPostId"] = lastPostId },
            TableUpdateMode.Replace,
            ct);

    /// <summary>Додає пост, якщо його ще немає. Повторний запуск не скидає статус оцінених постів.</summary>
    public async Task AddIfMissingAsync(PostEntity post, CancellationToken ct)
    {
        try
        {
            await _posts.AddEntityAsync(post, ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
        }
    }

    public async Task<IReadOnlyList<PostEntity>> GetAsync(IEnumerable<PostRef> refs, CancellationToken ct)
    {
        var posts = new List<PostEntity>();
        foreach (var (partitionKey, rowKey) in refs)
        {
            var result = await _posts.GetEntityIfExistsAsync<PostEntity>(partitionKey, rowKey, cancellationToken: ct);
            if (result.HasValue)
            {
                posts.Add(result.Value!);
            }
        }
        return posts;
    }

    /// <summary>Усі пости, опубліковані в [since, now]. Партиції — це дні, тому читаємо 2–3 партиції.</summary>
    public async Task<IReadOnlyList<PostEntity>> GetSinceAsync(DateTimeOffset since, DateTimeOffset now, CancellationToken ct)
    {
        var posts = new List<PostEntity>();
        for (var day = since.UtcDateTime.Date; day <= now.UtcDateTime.Date; day = day.AddDays(1))
        {
            var partition = day.ToString("yyyy-MM-dd");
            await foreach (var post in _posts.QueryAsync<PostEntity>(p => p.PartitionKey == partition, cancellationToken: ct))
            {
                if (post.PublishedAt >= since)
                {
                    posts.Add(post);
                }
            }
        }
        return posts;
    }

    public Task UpdateAsync(PostEntity post, CancellationToken ct) =>
        _posts.UpdateEntityAsync(post, post.ETag, TableUpdateMode.Replace, ct);

    /// <summary>Видаляє партиції (дні) старші за cutoff.</summary>
    public async Task DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct)
    {
        var cutoffKey = PostEntity.PartitionFor(cutoff);
        var old = _posts.QueryAsync<TableEntity>(
            TableClient.CreateQueryFilter($"PartitionKey lt {cutoffKey}"),
            select: ["PartitionKey", "RowKey"],
            cancellationToken: ct);

        var byPartition = new Dictionary<string, List<TableEntity>>();
        await foreach (var entity in old)
        {
            if (!byPartition.TryGetValue(entity.PartitionKey, out var list))
            {
                byPartition[entity.PartitionKey] = list = [];
            }
            list.Add(entity);
        }

        foreach (var entities in byPartition.Values)
        {
            foreach (var chunk in entities.Chunk(100))
            {
                await _posts.SubmitTransactionAsync(
                    chunk.Select(e => new TableTransactionAction(TableTransactionActionType.Delete, e)),
                    ct);
            }
        }
    }
}
