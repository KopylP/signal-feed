namespace PostAgent.Collecting;

public interface ISourceReader : IDisposable
{
    Task<SourceBatch> ReadAsync(string username, int lastPostId, CancellationToken ct);
}
