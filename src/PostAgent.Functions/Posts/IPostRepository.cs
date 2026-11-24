namespace PostAgent.Posts;

public interface IPostRepository
{
    Task AddIfMissingAsync(PostEntity post, CancellationToken ct);

    Task<IReadOnlyList<PostEntity>> GetAsync(IEnumerable<PostRef> refs, CancellationToken ct);

    Task<IReadOnlyList<PostEntity>> GetSinceAsync(DateTimeOffset since, DateTimeOffset now, CancellationToken ct);

    Task SaveScoresAsync(IEnumerable<PostEntity> posts, CancellationToken ct);

    Task MarkSentAsync(IEnumerable<PostEntity> posts, DateTimeOffset? alertedAt, CancellationToken ct);

    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct);
}
