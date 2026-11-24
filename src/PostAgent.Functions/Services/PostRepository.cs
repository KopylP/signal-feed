using Azure;
using Azure.Data.Tables;
using PostAgent.Models;

namespace PostAgent.Services;

/// <summary>Таблиці ChannelState (курсор читання джерела) і Posts.</summary>
public sealed class PostRepository(TableServiceClient tables)
{
    public const string PostsTable = "Posts";
    public const string ChannelStateTable = "ChannelState";

    private const string StatePartition = "source";
    private const int MaxTransactionSize = 100;

    private readonly TableClient _posts = tables.GetTableClient(PostsTable);
    private readonly TableClient _state = tables.GetTableClient(ChannelStateTable);

    public async Task<int> GetLastPostIdAsync(string username, CancellationToken ct)
    {
        var state = await _state.GetEntityIfExistsAsync<TableEntity>(StatePartition, username, cancellationToken: ct);
        return state.HasValue ? state.Value!.GetInt32("LastPostId") ?? 0 : 0;
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

    /// <summary>Пости, опубліковані в [since, now]. Партиції — це дні, тож запит читає 2–3 партиції.</summary>
    public async Task<IReadOnlyList<PostEntity>> GetSinceAsync(DateTimeOffset since, DateTimeOffset now, CancellationToken ct)
    {
        var (from, to) = (PostEntity.PartitionFor(since), PostEntity.PartitionFor(now));
        var posts = await _posts
            .QueryAsync<PostEntity>(TableClient.CreateQueryFilter($"PartitionKey ge {from} and PartitionKey le {to}"), cancellationToken: ct)
            .ToListAsync(ct);

        return posts.Where(p => p.PublishedAt >= since).ToList();
    }

    /// <summary>
    /// Записує оцінки: лише колонки оцінювання (Merge), з перевіркою ETag. Відповідь 412 означає,
    /// що пост уже оцінив інший виклик, тож його пропускаємо. Збій одного поста не зупиняє решту.
    /// </summary>
    public Task SaveScoresAsync(IEnumerable<PostEntity> posts, CancellationToken ct) =>
        WriteAllAsync(posts, async post =>
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
                await _posts.UpdateEntityAsync(columns, post.ETag, TableUpdateMode.Merge, ct);
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
            }
        });

    /// <summary>
    /// Позначає пости доставленими: лише Status і AlertedAt, без перевірки ETag. Перехід Scored → Sent
    /// однобічний і повторюваний, тому конфлікти тут не потрібні. Збій одного поста не зупиняє решту.
    /// </summary>
    public Task MarkDeliveredAsync(IEnumerable<PostEntity> posts, CancellationToken ct) =>
        WriteAllAsync(posts, post =>
        {
            var columns = new TableEntity(post.PartitionKey, post.RowKey)
            {
                [nameof(PostEntity.Status)] = post.Status.ToString(),
            };
            if (post.AlertedAt is not null)
            {
                columns[nameof(PostEntity.AlertedAt)] = post.AlertedAt;
            }

            return _posts.UpdateEntityAsync(columns, ETag.All, TableUpdateMode.Merge, ct);
        });

    /// <summary>Виконує запис для кожного поста; помилки збирає й кидає разом наприкінці.</summary>
    private static async Task WriteAllAsync(IEnumerable<PostEntity> posts, Func<PostEntity, Task> write)
    {
        var errors = new List<Exception>();
        foreach (var post in posts)
        {
            try
            {
                await write(post);
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

    /// <summary>Видаляє партиції (дні) старші за день cutoff. Повертає кількість видалених постів.</summary>
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
                batch.Select(e => new TableTransactionAction(TableTransactionActionType.Delete, e)),
                ct);
        }

        return old.Count;
    }
}
