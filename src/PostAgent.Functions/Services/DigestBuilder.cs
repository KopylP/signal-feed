using PostAgent.Models;

namespace PostAgent.Services;

public sealed record DigestItem(PostEntity Post, bool IsInteresting);

/// <summary>Вибирає пости для денного дайджесту (чиста функція).</summary>
public static class DigestBuilder
{
    public static IReadOnlyList<DigestItem> Build(IEnumerable<PostEntity> posts, AgentOptions options, DateTimeOffset now)
    {
        var scored = posts.Where(p => p.Status == PostStatus.Scored && p.Score is not null).ToList();

        var candidates = scored.Where(p => p.PublishedAt >= now.AddHours(-24)).ToList();
        if (candidates.Count == 0)
        {
            candidates = scored.Where(p => p.PublishedAt >= now.AddHours(-48)).ToList();
        }

        return candidates
            .Where(p => p.DuplicateOf is null)
            .OrderByDescending(p => p.Score)
            .ThenByDescending(p => p.PublishedAt)
            .Take(options.DigestSize)
            .Select(p => new DigestItem(p, p.Score >= options.InterestingThreshold))
            .ToList();
    }
}
