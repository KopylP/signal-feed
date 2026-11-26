namespace PostAgent.Collecting;

public interface ISourceCursors
{
    Task<int> GetLastPostIdAsync(string username, CancellationToken ct);

    Task SetLastPostIdAsync(string username, int lastPostId, CancellationToken ct);

    // Forgets the cursors of sources that are no longer listed, so adding one back later starts as a new source.
    Task DeleteExceptAsync(IReadOnlyCollection<string> keep, CancellationToken ct);
}
