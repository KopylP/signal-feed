using Microsoft.Extensions.AI;
using PostAgent.Shared.Ai;
using PostAgent.Shared.Infrastructure;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;

namespace PostAgent.Deduplication;

// The stage between collection and scoring (ADR-0023, ADR-0024): embeds the new posts, drops the near-duplicates of posts handled before so the
// model never scores them, and queues only the rest for scoring. A failed embedding call never holds scoring back: the posts go on without vectors.
public sealed class PostDeduplicator(
    IPostRepository posts,
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
    [FromKeyedServices(StorageNames.ScoringQueue)] IPostQueue scoring,
    IDeduplicationLock checkLock,
    IEffectiveOptions options,
    TimeProvider time,
    ILogger<PostDeduplicator> log)
{
    public async Task DeduplicateAsync(IReadOnlyList<PostRef> refs, CancellationToken ct)
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
            // The model call above runs in parallel; the check and the save run one message at a time, so a vector in the table always
            // comes with its decision, and parallel messages see each other's posts.
            await using (await checkLock.AcquireAsync(ct))
            {
                duplicates = await MarkDuplicatesAsync(batch, settings, ct);
                // Saved before the queue: a retry finds the vectors and the duplicates (no longer New) and only queues again.
                await posts.SaveEmbeddingsAsync(batch, ct);
            }
        }

        await scoring.EnqueueAsync(batch.Except(duplicates).Select(p => p.ToRef()).ToList(), settings.ScoringBatchSize, ct);
        log.LogInformation("Deduplication: {Posts} posts, {Duplicates} near-duplicates not scored", batch.Count, duplicates.Count);
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

    private async Task<IReadOnlyList<PostEntity>> MarkDuplicatesAsync(List<PostEntity> batch, AgentOptions settings, CancellationToken ct)
    {
        var window = TimeSpan.FromHours(settings.DuplicateWindowHours);
        var inBatch = batch.Select(p => p.ToRef()).ToHashSet();
        var stored = await posts.GetSinceAsync(batch.Min(p => p.PublishedAt) - window, time.GetUtcNow(), ct);
        var known = stored.Where(p => !inBatch.Contains(p.ToRef())).Concat(batch).ToList();
        return DuplicateFinder.Mark(batch, known, settings.DuplicateSimilarityThreshold, window);
    }
}
