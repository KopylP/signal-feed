namespace PostAgent.Shared.Cleanup;

public interface ICleanupRequestQueue
{
    Task EnqueueAsync(CleanupRequest request, CancellationToken ct);
}
