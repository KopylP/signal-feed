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

    // З якого моменту читати пости для дайджесту.
    public static DateTimeOffset QueryStart(DateTimeOffset now) => now - FallbackWindow;

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
            .Take(options.DigestSize)
            .Select(p => new DigestItem(p, p.Score >= options.InterestingThreshold))
            .ToList();
    }

    private static List<PostEntity> PublishedWithin(IEnumerable<PostEntity> posts, TimeSpan window, DateTimeOffset now) =>
        posts.Where(p => p.PublishedAt >= now - window).ToList();
}
