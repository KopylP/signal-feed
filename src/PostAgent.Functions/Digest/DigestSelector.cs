using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;

namespace PostAgent.Digest;

// Вибирає пости для денного дайджесту (чиста функція).
public static class DigestSelector
{
    // Доба плюс 2 години запасу: пост, опублікований в останню годину перед минулим дайджестом,
    // на той момент ще не був оцінений. Повторів немає, бо надіслані пости мають статус Sent.
    private static readonly TimeSpan Window = TimeSpan.FromHours(26);
    private static readonly TimeSpan FallbackWindow = TimeSpan.FromHours(48);

    // Collection runs hourly and scoring takes minutes, so a post still unscored after this has failed scoring.
    private static readonly TimeSpan ScoringGrace = TimeSpan.FromHours(2);

    // З якого моменту читати пости для дайджесту.
    public static DateTimeOffset QueryStart(DateTimeOffset now) => now - FallbackWindow;

    public static IReadOnlyList<DigestItem> Build(IEnumerable<PostEntity> posts, AgentOptions options, DateTimeOffset now) =>
        Candidates(posts, now)
            .Where(p => p.DuplicateOf is null)
            .OrderByDescending(p => p.Score)
            .ThenByDescending(p => p.PublishedAt)
            .Take(options.DigestSize)
            .Select(p => new DigestItem(p, p.Score >= options.InterestingThreshold))
            .ToList();

    // The candidates that are not in the digest, counted by reason: the "can be skipped" line.
    public static DigestSkipped CountSkipped(IEnumerable<PostEntity> posts, IReadOnlyList<DigestItem> selected, DateTimeOffset now)
    {
        var chosen = selected.Select(i => i.Post.RowKey).ToHashSet();
        var left = Candidates(posts, now).Where(p => !chosen.Contains(p.RowKey)).ToList();
        var duplicates = left.Count(p => p.DuplicateOf is not null);
        return new DigestSkipped(duplicates, left.Count - duplicates);
    }

    // Posts whose scoring failed (the message went to the poison queue): without this count
    // a broken model key would look like a quiet day.
    public static int CountUnscored(IEnumerable<PostEntity> posts, DateTimeOffset now) =>
        posts.Count(p => p.Status == PostStatus.New && p.PublishedAt >= now - Window && p.PublishedAt < now - ScoringGrace);

    private static List<PostEntity> Candidates(IEnumerable<PostEntity> posts, DateTimeOffset now)
    {
        var scored = posts.Where(p => p.Status == PostStatus.Scored && p.Score is not null).ToList();
        var candidates = PublishedWithin(scored, Window, now);
        return candidates.Count == 0 ? PublishedWithin(scored, FallbackWindow, now) : candidates;
    }

    private static List<PostEntity> PublishedWithin(IEnumerable<PostEntity> posts, TimeSpan window, DateTimeOffset now) =>
        posts.Where(p => p.PublishedAt >= now - window).ToList();
}
