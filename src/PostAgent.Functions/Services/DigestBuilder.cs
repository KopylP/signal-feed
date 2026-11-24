using PostAgent.Models;

namespace PostAgent.Services;

public sealed record DigestItem(PostEntity Post, bool IsInteresting);

/// <summary>Вибирає пости для денного дайджесту (чиста функція).</summary>
public static class DigestBuilder
{
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);
    public static readonly TimeSpan FallbackWindow = TimeSpan.FromHours(48);

    public static IReadOnlyList<DigestItem> Build(IEnumerable<PostEntity> posts, AgentOptions options, DateTimeOffset now)
    {
        var scored = posts.Where(p => p.Status == PostStatus.Scored && p.Score is not null).ToList();
        var candidates = PublishedWithin(scored, Window, now);
        if (candidates.Count == 0)
        {
            candidates = PublishedWithin(scored, FallbackWindow, now);
        }

        return candidates
            .Where(p => p.DuplicateOf is null)
            .OrderByDescending(p => p.Score)
            .ThenByDescending(p => p.PublishedAt)
            .Take(Math.Min(options.DigestSize, TelegramMessages.MaxItems))
            .Select(p => new DigestItem(p, p.Score >= options.InterestingThreshold))
            .ToList();
    }

    private static List<PostEntity> PublishedWithin(IEnumerable<PostEntity> posts, TimeSpan window, DateTimeOffset now) =>
        posts.Where(p => p.PublishedAt >= now - window).ToList();
}
