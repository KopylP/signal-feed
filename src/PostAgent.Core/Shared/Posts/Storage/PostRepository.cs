using Azure;
using Azure.Data.Tables;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Shared.Posts.Storage;

public sealed class PostRepository(TableServiceClient tables) : IPostRepository
{

    private const int MaxTransactionSize = 100;

    private readonly TableClient _posts = tables.GetTableClient(StorageNames.PostsTable);

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
            if (result.HasValue && result.Value is { } post)
            {
                posts.Add(post);
            }
        }
        return posts;
    }

    public async Task<IReadOnlyList<PostEntity>> GetSinceAsync(DateTimeOffset since, DateTimeOffset now, CancellationToken ct)
    {
        var (from, to) = (PostEntity.PartitionFor(since), PostEntity.PartitionFor(now));
        var posts = await _posts
            .QueryAsync<PostEntity>(TableClient.CreateQueryFilter($"PartitionKey ge {from} and PartitionKey le {to}"), cancellationToken: ct)
            .ToListAsync(ct);

        return posts.Where(p => p.PublishedAt >= since).ToList();
    }

    public Task SaveEmbeddingsAsync(IEnumerable<PostEntity> posts, CancellationToken ct) =>
        WriteAllAsync(posts, async (post, token) =>
        {
            var columns = new TableEntity(post.PartitionKey, post.RowKey)
            {
                [nameof(PostEntity.Embedding)] = post.Embedding,
                [nameof(PostEntity.Status)] = post.Status.ToString(),
            };
            if (post.DuplicateOf is not null)
            {
                columns[nameof(PostEntity.DuplicateOf)] = post.DuplicateOf;
            }

            try
            {
                await _posts.UpdateEntityAsync(columns, post.ETag, TableUpdateMode.Merge, token);
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
                // Written meanwhile by another delivery of the same posts: that result stands, and the post is either queued by it or marked Scored.
            }
        }, ct);

    public Task SaveScoresAsync(IEnumerable<PostEntity> posts, CancellationToken ct) =>
        WriteAllAsync(posts, async (post, token) =>
        {
            var columns = new TableEntity(post.PartitionKey, post.RowKey)
            {
                [nameof(PostEntity.Score)] = post.Score,
                [nameof(PostEntity.Topic)] = post.Topic,
                [nameof(PostEntity.Reason)] = post.Reason,
                [nameof(PostEntity.Status)] = post.Status.ToString(),
            };
            if (post.DuplicateOf is not null)
            {
                columns[nameof(PostEntity.DuplicateOf)] = post.DuplicateOf;
            }

            try
            {
                await _posts.UpdateEntityAsync(columns, post.ETag, TableUpdateMode.Merge, token);
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
            }
        }, ct);

    public Task MarkAlertDecidedAsync(IEnumerable<PostEntity> posts, CancellationToken ct) =>
        WriteAllAsync(posts, async (post, token) =>
        {
            var columns = new TableEntity(post.PartitionKey, post.RowKey);
            if (post.AlertedAt is not null)
            {
                columns[nameof(PostEntity.AlertedAt)] = post.AlertedAt;
            }

            if (post.WatchAlertedAt is not null)
            {
                columns[nameof(PostEntity.WatchAlertedAt)] = post.WatchAlertedAt;
            }

            await _posts.UpdateEntityAsync(columns, ETag.All, TableUpdateMode.Merge, token);
        }, ct);

    public Task MarkSentAsync(IEnumerable<PostEntity> posts, DateTimeOffset? alertedAt, CancellationToken ct) =>
        WriteAllAsync(posts, async (post, token) =>
        {
            var columns = new TableEntity(post.PartitionKey, post.RowKey)
            {
                [nameof(PostEntity.Status)] = nameof(PostStatus.Sent),
            };
            if (alertedAt is not null)
            {
                columns[nameof(PostEntity.AlertedAt)] = alertedAt;
            }

            try
            {
                await _posts.UpdateEntityAsync(columns, ETag.All, TableUpdateMode.Merge, token);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Пост уже видалив Cleanup (дайджест чи алерт перетнув межу доби): позначати нічого.
            }
        }, ct);

    private static async Task WriteAllAsync(IEnumerable<PostEntity> posts, Func<PostEntity, CancellationToken, Task> write, CancellationToken ct)
    {
        var errors = new List<Exception>();
        foreach (var post in posts)
        {
            try
            {
                await write(post, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add(ex);
            }
        }

        if (errors.Count > 0)
        {
            throw new AggregateException("Some posts could not be saved.", errors);
        }
    }

    public async Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct)
    {
        var cutoffPartition = PostEntity.PartitionFor(cutoff);
        var old = await _posts
            .QueryAsync<TableEntity>(
                TableClient.CreateQueryFilter($"PartitionKey lt {cutoffPartition}"),
                select: ["PartitionKey", "RowKey"],
                cancellationToken: ct)
            .ToListAsync(ct);

        // Транзакція Table Storage працює в межах однієї партиції й до 100 операцій.
        foreach (var batch in old.GroupBy(e => e.PartitionKey).SelectMany(g => g.Chunk(MaxTransactionSize)))
        {
            await _posts.SubmitTransactionAsync(
                batch.Select(e => new TableTransactionAction(TableTransactionActionType.Delete, e, ETag.All)),
                ct);
        }

        return old.Count;
    }
}
