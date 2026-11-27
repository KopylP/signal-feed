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

    public Task AddIfMissingAsync(PostEntity post, CancellationToken ct)
    {
        Added.Add(post);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PostEntity>> GetAsync(IEnumerable<PostRef> refs, CancellationToken ct)
    {
        var wanted = refs.ToHashSet();
        return Task.FromResult<IReadOnlyList<PostEntity>>(Posts.Where(p => wanted.Contains(p.ToRef())).ToList());
    }

    public Task<IReadOnlyList<PostEntity>> GetSinceAsync(DateTimeOffset since, DateTimeOffset now, CancellationToken ct)
    {
        QueriedSince = since;
        return Task.FromResult<IReadOnlyList<PostEntity>>(Posts.Where(p => p.PublishedAt >= since).ToList());
    }

    public Task SaveEmbeddingsAsync(IEnumerable<PostEntity> posts, CancellationToken ct)
    {
        EmbeddingsSaved.AddRange(posts);
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
}
