using PostAgent.Scoring;
using PostAgent.Shared.Posts;

namespace PostAgent.Tests.Fakes;

// Scores all posts except the given ids: the "model skipped" them.
internal sealed class FakePostScorer(params string[] skippedIds) : IPostScorer
{
    public int Calls { get; private set; }

    public Task<IReadOnlyList<string>> ScoreAsync(IReadOnlyList<PostEntity> posts, CancellationToken ct)
    {
        Calls++;
        var ids = posts.Select(p => p.RowKey).ToHashSet();
        foreach (var post in posts.Where(p => !skippedIds.Contains(p.RowKey)))
        {
            post.ApplyScore(new PostScore(post.RowKey, 8, "Тема", "Причина", null), ids);
        }
        return Task.FromResult<IReadOnlyList<string>>(skippedIds.Where(ids.Contains).ToList());
    }
}
