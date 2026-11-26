namespace PostAgent.Shared.Stats;

// What stays of a day after its posts are deleted: counts and a few names, never the text of a post. Topics and titles come from
// channels through the model, so they are data wherever they are shown. Days are UTC dates of publication (the partition of Posts).
public sealed record DailyStats(
    string Day,
    int Collected,
    int Scored,
    int Interesting,
    int Duplicates,
    double AverageScore,
    IReadOnlyList<SourceStats> Sources,
    IReadOnlyList<TopicStats> Topics,
    IReadOnlyList<TopPost> Top);

public sealed record SourceStats(string Source, int Posts, double AverageScore, int Interesting);

public sealed record TopicStats(string Topic, int Posts, double AverageScore);

public sealed record TopPost(string PostId, string Source, string? Topic, int Score, string Url);
