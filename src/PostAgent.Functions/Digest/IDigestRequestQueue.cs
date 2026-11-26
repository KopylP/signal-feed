namespace PostAgent.Digest;

public interface IDigestRequestQueue
{
    Task EnqueueAsync(DigestRequest request, CancellationToken ct);
}
