using Azure;
using Azure.Data.Tables;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Shared.Posts.Storage;

public sealed class PostRepository(TableServiceClient tables, ILogger<PostRepository> log) : IPostRepository
{
    private const int MaxTransactionSize = 100;

    // Point reads run in small parallel groups: one round trip per post in a row is slow for a big batch, an unbounded fan-out is rude to the table.
    private const int ReadParallelism = 20;

    private readonly TableClient _posts = tables.GetTableClient(StorageNames.PostsTable);

    public async Task AddIfMissingAsync(PostEntity post, CancellationToken ct)
    {
        try
        {
            await _posts.AddEntityAsync(post, ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            // Already collected: adding is idempotent.
        }
    }

    public async Task<IReadOnlyList<PostEntity>> GetAsync(IEnumerable<PostRef> refs, CancellationToken ct)
    {
        var posts = new List<PostEntity>();
        foreach (var group in refs.Chunk(ReadParallelism))
        {
            var results = await Task.WhenAll(group.Select(r => _posts.GetEntityIfExistsAsync<PostEntity>(r.PartitionKey, r.RowKey, cancellationToken: ct)));
            foreach (var result in results)
            {
                if (result.HasValue && result.Value is { } post)
                {
                    posts.Add(post);
                }
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
        MergeAsync(posts, withDuplicateOf: true, ifUnchanged: true, post => new()
        {
            [nameof(PostEntity.Embedding)] = post.Embedding,
            [nameof(PostEntity.Status)] = post.Status.ToString(),
        }, ct);

    public Task SaveScoresAsync(IEnumerable<PostEntity> posts, CancellationToken ct) =>
        MergeAsync(posts, withDuplicateOf: true, ifUnchanged: true, post => new()
        {
            [nameof(PostEntity.Score)] = post.Score,
            [nameof(PostEntity.Topic)] = post.Topic,
            [nameof(PostEntity.Reason)] = post.Reason,
            [nameof(PostEntity.Status)] = post.Status.ToString(),
        }, ct);

    public Task MarkAlertDecidedAsync(IEnumerable<PostEntity> posts, CancellationToken ct) =>
        MergeAsync(posts, withDuplicateOf: false, ifUnchanged: false, post =>
        {
            var columns = new TableEntity();
            if (post.AlertedAt is not null)
            {
                columns[nameof(PostEntity.AlertedAt)] = post.AlertedAt;
            }

            if (post.WatchAlertedAt is not null)
            {
                columns[nameof(PostEntity.WatchAlertedAt)] = post.WatchAlertedAt;
            }

            return columns;
        }, ct);

    public Task MarkSentAsync(IEnumerable<PostEntity> posts, DateTimeOffset? alertedAt, CancellationToken ct) =>
        MergeAsync(posts, withDuplicateOf: false, ifUnchanged: false, _ =>
        {
            var columns = new TableEntity { [nameof(PostEntity.Status)] = nameof(PostStatus.Sent) };
            if (alertedAt is not null)
            {
                columns[nameof(PostEntity.AlertedAt)] = alertedAt;
            }

            return columns;
        }, ct);

    // Merges only the given columns into each post. ifUnchanged makes the write fail when the row was changed after it was read;
    // otherwise it overwrites those columns whatever the row version is.
    private Task MergeAsync(
        IEnumerable<PostEntity> posts, bool withDuplicateOf, bool ifUnchanged, Func<PostEntity, TableEntity> columnsOf, CancellationToken ct) =>
        WriteAllAsync(posts, async (post, token) =>
        {
            var columns = columnsOf(post);
            columns.PartitionKey = post.PartitionKey;
            columns.RowKey = post.RowKey;
            if (withDuplicateOf && post.DuplicateOf is not null)
            {
                columns[nameof(PostEntity.DuplicateOf)] = post.DuplicateOf;
            }

            try
            {
                await _posts.UpdateEntityAsync(columns, ifUnchanged ? post.ETag : ETag.All, TableUpdateMode.Merge, token);
            }
            catch (RequestFailedException ex) when (ex.Status is 412 or 404)
            {
                // 412: another delivery of the same posts wrote the row first, and that result stands.
                // 404: cleanup already deleted the post (a digest or an alert crossed the day boundary), so there is nothing to update.
                log.LogInformation("Post {PostKey} was not updated: it changed or was deleted meanwhile ({Status})", $"{post.PartitionKey}/{post.RowKey}", ex.Status);
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

        // A Table Storage transaction works within one partition and up to 100 operations.
        foreach (var batch in old.GroupBy(e => e.PartitionKey).SelectMany(g => g.Chunk(MaxTransactionSize)))
        {
            await _posts.SubmitTransactionAsync(
                batch.Select(e => new TableTransactionAction(TableTransactionActionType.Delete, e, ETag.All)),
                ct);
        }

        return old.Count;
    }
}
