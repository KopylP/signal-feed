using PostAgent.Shared.Posts;

namespace PostAgent.Embedding;

// Near-duplicates across sources and runs, found by embeddings before the model scores anything (ADR-0023).
// Only single messages are compared: two bundles of one busy group read alike without being the same news.
public static class DuplicateFinder
{
    // Marks every post of the batch that repeats an earlier one (published before it, or at the same moment with a smaller id):
    // DuplicateOf is the most similar such post, and the status Scored keeps it out of scoring. known holds the batch itself too.
    public static IReadOnlyList<PostEntity> Mark(IReadOnlyList<PostEntity> batch, IReadOnlyList<PostEntity> known, double threshold)
    {
        var duplicates = new List<PostEntity>();
        foreach (var post in batch.Where(IsComparable).OrderBy(p => p.PublishedAt).ThenBy(p => p.RowKey, StringComparer.Ordinal))
        {
            var original = known
                .Where(o => IsComparable(o) && IsEarlier(o, post))
                .Select(o => (Post: o, Similarity: PostVectors.Similarity(o.Embedding, post.Embedding)))
                .Where(x => x.Similarity >= threshold)
                .OrderByDescending(x => x.Similarity)
                .Select(x => x.Post)
                .FirstOrDefault();
            if (original is null)
            {
                continue;
            }

            post.DuplicateOf = original.RowKey;
            post.Status = PostStatus.Scored;
            duplicates.Add(post);
        }

        return duplicates;
    }

    private static bool IsComparable(PostEntity post) => post.Embedding is not null && post.MessageCount == 1 && post.DuplicateOf is null;

    private static bool IsEarlier(PostEntity a, PostEntity b) =>
        a.PublishedAt < b.PublishedAt || (a.PublishedAt == b.PublishedAt && string.CompareOrdinal(a.RowKey, b.RowKey) < 0);
}
