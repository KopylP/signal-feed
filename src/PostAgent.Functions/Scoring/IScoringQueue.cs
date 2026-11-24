using PostAgent.Shared.Posts;

namespace PostAgent.Scoring;

public interface IScoringQueue
{
    Task EnqueueAsync(IReadOnlyList<PostRef> refs, int batchSize, CancellationToken ct);
}
