namespace PostAgent.Shared.Posts;

// A stage of the post pipeline (posts-collected, to-score): references only, the post itself stays in the table.
// Resolved by the queue name: [FromKeyedServices(StorageNames.CollectedQueue)].
public interface IPostQueue
{
    Task EnqueueAsync(IReadOnlyList<PostRef> refs, int batchSize, CancellationToken ct);
}
