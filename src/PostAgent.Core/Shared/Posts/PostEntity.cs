using Azure;
using Azure.Data.Tables;

namespace PostAgent.Shared.Posts;

public sealed class PostEntity : ITableEntity
{
    private const string PartitionFormat = "yyyy-MM-dd";

    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Source { get; set; } = string.Empty;
    public SourceType SourceType { get; set; }
    public int PostId { get; set; }
    public string? Author { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; set; }
    public PostStatus Status { get; set; } = PostStatus.New;

    public int MessageCount { get; set; } = 1;

    public int? Score { get; set; }
    public string? Topic { get; set; }
    public string? Reason { get; set; }
    public string? DuplicateOf { get; set; }
    public DateTimeOffset? AlertedAt { get; set; }

    public static string PartitionFor(DateTimeOffset publishedAt) =>
        publishedAt.UtcDateTime.ToString(PartitionFormat);

    public PostRef ToRef() => new(PartitionKey, RowKey);

    public void ApplyScore(PostScore score, IReadOnlySet<string> batchIds)
    {
        Score = Math.Clamp(score.Score, 0, 10);
        Topic = score.Topic;
        Reason = score.Reason;
        DuplicateOf = score.DuplicateOf is { } id && id != RowKey && batchIds.Contains(id) ? id : null;
        Status = PostStatus.Scored;
    }
}
