using PostAgent.Shared.Posts;

namespace PostAgent.Tests.Fakes;

internal sealed class InMemoryPostRepository(params PostEntity[] posts) : IPostRepository
{
    public List<PostEntity> Posts { get; } = [.. posts];
    public List<PostEntity> Added { get; } = [];
    public List<PostEntity> ScoresSaved { get; } = [];
    public List<PostEntity> EmbeddingsSaved { get; } = [];
    public List<(IReadOnlyList<PostEntity> Posts, DateTimeOffset? AlertedAt)> SentMarks { get; } = [];
    public DateTimeOffset? QueriedSince { get; private set; }
    public DateTimeOffset? DeletedBefore { get; private set; }
    public int DeletedCount { get; set; }

    // Like the table: a read returns copies, and only a save changes what is stored (SaveEmbeddingsAsync applies its columns).
    public bool CopyOnRead { get; init; }

    public Task AddIfMissingAsync(PostEntity post, CancellationToken ct)
    {
        Added.Add(post);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PostEntity>> GetAsync(IEnumerable<PostRef> refs, CancellationToken ct)
    {
        var wanted = refs.ToHashSet();
        return Task.FromResult<IReadOnlyList<PostEntity>>(Posts.Where(p => wanted.Contains(p.ToRef())).Select(Read).ToList());
    }

    public Task<IReadOnlyList<PostEntity>> GetSinceAsync(DateTimeOffset since, DateTimeOffset now, CancellationToken ct)
    {
        QueriedSince = since;
        return Task.FromResult<IReadOnlyList<PostEntity>>(Posts.Where(p => p.PublishedAt >= since).Select(Read).ToList());
    }

    public Task SaveEmbeddingsAsync(IEnumerable<PostEntity> posts, CancellationToken ct)
    {
        foreach (var post in posts)
        {
            EmbeddingsSaved.Add(post);
            if (CopyOnRead && Posts.Find(p => p.ToRef() == post.ToRef()) is { } stored)
            {
                (stored.Embedding, stored.Status, stored.DuplicateOf) = (post.Embedding, post.Status, post.DuplicateOf ?? stored.DuplicateOf);
            }
        }

        return Task.CompletedTask;
    }

    public Task SaveScoresAsync(IEnumerable<PostEntity> posts, CancellationToken ct)
    {
        ScoresSaved.AddRange(posts);
        return Task.CompletedTask;
    }

    public Task MarkSentAsync(IEnumerable<PostEntity> posts, DateTimeOffset? alertedAt, CancellationToken ct)
    {
        SentMarks.Add((posts.ToList(), alertedAt));
        return Task.CompletedTask;
    }

    public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct)
    {
        DeletedBefore = cutoff;
        return Task.FromResult(DeletedCount);
    }

    private PostEntity Read(PostEntity post) => !CopyOnRead ? post : new PostEntity
    {
        PartitionKey = post.PartitionKey,
        RowKey = post.RowKey,
        Source = post.Source,
        SourceType = post.SourceType,
        PostId = post.PostId,
        Author = post.Author,
        Url = post.Url,
        Text = post.Text,
        PublishedAt = post.PublishedAt,
        Status = post.Status,
        MessageCount = post.MessageCount,
        Score = post.Score,
        Topic = post.Topic,
        Reason = post.Reason,
        DuplicateOf = post.DuplicateOf,
        AlertedAt = post.AlertedAt,
        Embedding = post.Embedding,
    };
}
