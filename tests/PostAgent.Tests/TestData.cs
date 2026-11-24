using PostAgent;
using PostAgent.Models;

namespace PostAgent.Tests;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 11, 24, 8, 0, 0, TimeSpan.Zero);

    public static AgentOptions Options(Action<AgentOptions>? configure = null)
    {
        var options = new AgentOptions { AlertSources = "chat" };
        configure?.Invoke(options);
        return options;
    }

    public static string Text(int length = 100) => new('x', length);

    public static TgMessage Message(int id, string? text = null, bool service = false, int engagement = 0,
        string? author = null, DateTimeOffset? date = null) =>
        new(id, date ?? Now.AddMinutes(-id), text ?? Text(), author, engagement, service);

    public static PostEntity Post(string rowKey = "src_1", int? score = 5, double hoursAgo = 1, PostStatus status = PostStatus.Scored,
        SourceType type = SourceType.Channel, string source = "src", int messages = 1, string? duplicateOf = null,
        DateTimeOffset? alertedAt = null)
    {
        var published = Now.AddHours(-hoursAgo);
        return new PostEntity
        {
            PartitionKey = PostEntity.PartitionFor(published),
            RowKey = rowKey,
            Source = source,
            SourceType = type,
            Url = $"https://t.me/{source}/1",
            Text = Text(),
            PublishedAt = published,
            Status = status,
            Score = score,
            Topic = "Тема",
            Reason = "Причина",
            MessageCount = messages,
            DuplicateOf = duplicateOf,
            AlertedAt = alertedAt,
        };
    }
}
