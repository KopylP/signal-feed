using PostAgent.Shared.Posts;

namespace PostAgent.Scoring;

public interface IPostScorer
{
    // Повертає id постів, для яких модель оцінки не дала.
    Task<IReadOnlyList<string>> ScoreAsync(IReadOnlyList<PostEntity> posts, CancellationToken ct);
}
