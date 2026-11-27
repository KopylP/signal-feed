using Microsoft.Extensions.AI;
using PostAgent.Shared.Ai;
using PostAgent.Shared.Infrastructure;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;

namespace PostAgent.Embedding;

// The stage between collection and scoring (ADR-0023): embeds the new posts, drops the near-duplicates of earlier posts so the model
// never scores them, and queues the rest for scoring. A failed embedding call never holds scoring back: the posts go on without vectors.
public sealed class PostEmbedder(
    IPostRepository posts,
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
    [FromKeyedServices(StorageNames.ScoringQueue)] IPostQueue scoring,
    IEffectiveOptions options,
    TimeProvider time,
    ILogger<PostEmbedder> log)
{
    // An earlier post is looked for among about the last day, the window of the digest.
    public static readonly TimeSpan DuplicateWindow = TimeSpan.FromHours(26);

    public async Task EmbedAsync(IReadOnlyList<PostRef> refs, CancellationToken ct)
    {
        var batch = (await posts.GetAsync(refs, ct)).Where(p => p.Status == PostStatus.New).ToList();
        if (batch.Count == 0)
        {
            return;
        }

        var settings = await options.GetAsync(ct);
        IReadOnlyList<PostEntity> duplicates = [];
        if (await TryEmbedAsync(batch, ct))
        {
            duplicates = await MarkDuplicatesAsync(batch, settings.DuplicateSimilarityThreshold, ct);
            // Saved before the queue: a retry finds the vectors and the duplicates (no longer New) and only queues again.
            await posts.SaveEmbeddingsAsync(batch, ct);
        }

        await scoring.EnqueueAsync(batch.Except(duplicates).Select(p => p.ToRef()).ToList(), settings.ScoringBatchSize, ct);
        log.LogInformation("Embedding stage: {Posts} posts, {Duplicates} near-duplicates not scored", batch.Count, duplicates.Count);
    }

    private async Task<bool> TryEmbedAsync(List<PostEntity> batch, CancellationToken ct)
    {
        var missing = batch.Where(p => p.Embedding is null).ToList();
        if (missing.Count == 0)
        {
            return true;
        }

        try
        {
            var vectors = await embeddings.EmbedAsync(missing.Select(p => p.Text), ct);
            if (vectors.Count != missing.Count)
            {
                throw new InvalidOperationException($"{vectors.Count} vectors for {missing.Count} posts");
            }

            foreach (var (post, vector) in missing.Zip(vectors))
            {
                post.Embedding = PostVectors.ToBytes(vector);
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Embedding {Count} posts failed, they go to scoring without vectors", missing.Count);
            return false;
        }
    }

    private async Task<IReadOnlyList<PostEntity>> MarkDuplicatesAsync(List<PostEntity> batch, double threshold, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var inBatch = batch.Select(p => p.ToRef()).ToHashSet();
        var known = (await posts.GetSinceAsync(now - DuplicateWindow, now, ct)).Where(p => !inBatch.Contains(p.ToRef())).Concat(batch).ToList();
        return DuplicateFinder.Mark(batch, known, threshold);
    }
}
