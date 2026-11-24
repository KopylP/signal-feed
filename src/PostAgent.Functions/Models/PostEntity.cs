using Azure;
using Azure.Data.Tables;

namespace PostAgent.Models;

/// <summary>Рядок таблиці Posts. PartitionKey = дата публікації (UTC), RowKey = "{username}_{postId}".</summary>
public sealed class PostEntity : ITableEntity
{
    private const string PartitionFormat = "yyyy-MM-dd";

    public string PartitionKey { get; set; } = "";
    public string RowKey { get; set; } = "";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Source { get; set; } = "";
    public SourceType SourceType { get; set; }
    public PostKind Kind { get; set; }
    public int PostId { get; set; }
    public string? Author { get; set; }
    public string Url { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTimeOffset PublishedAt { get; set; }
    public PostStatus Status { get; set; } = PostStatus.New;

    /// <summary>Кількість повідомлень у пачці (1 для звичайного посту).</summary>
    public int MessageCount { get; set; } = 1;
    public int Engagement { get; set; }

    public int? Score { get; set; }
    public string? Topic { get; set; }
    public string? Reason { get; set; }
    public string? DuplicateOf { get; set; }
    public DateTimeOffset? AlertedAt { get; set; }

    public static string PartitionFor(DateTimeOffset publishedAt) =>
        publishedAt.UtcDateTime.ToString(PartitionFormat);

    public PostRef ToRef() => new(PartitionKey, RowKey);

    public void ApplyScore(PostScore score)
    {
        Score = Math.Clamp(score.Score, 0, 10);
        Topic = score.Topic;
        Reason = score.Reason;
        DuplicateOf = string.IsNullOrWhiteSpace(score.DuplicateOf) || score.DuplicateOf == RowKey ? null : score.DuplicateOf;
        Status = PostStatus.Scored;
    }

    public void MarkSent(DateTimeOffset? alertedAt = null)
    {
        Status = PostStatus.Sent;
        AlertedAt = alertedAt ?? AlertedAt;
    }
}

/// <summary>Посилання на пост. Саме воно (а не текст) лежить у черзі to-score.</summary>
public sealed record PostRef(string PartitionKey, string RowKey);
