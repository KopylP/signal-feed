namespace PostAgent.Embedding;

// One duplicate check at a time across all instances: each sees the vectors and decisions of the ones before it.
public interface IEmbeddingLock
{
    // Waits while another message holds it; disposing the handle releases it.
    Task<IAsyncDisposable> AcquireAsync(CancellationToken ct);
}
