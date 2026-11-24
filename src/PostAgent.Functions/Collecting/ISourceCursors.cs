namespace PostAgent.Collecting;

public interface ISourceCursors
{
    Task<int> GetLastPostIdAsync(string username, CancellationToken ct);

    Task SetLastPostIdAsync(string username, int lastPostId, CancellationToken ct);
}
