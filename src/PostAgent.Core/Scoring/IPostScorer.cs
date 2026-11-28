using PostAgent.Shared.Posts;

namespace PostAgent.Scoring;

public interface IPostScorer
{
    // Returns the ids of the posts the scoring model gave no score for.
    Task<IReadOnlyList<string>> ScoreAsync(IReadOnlyList<PostEntity> posts, CancellationToken ct);
}
