namespace PostAgent.Models;

/// <summary>Повідомлення Telegram у вигляді, незалежному від бібліотеки.</summary>
public sealed record TgMessage(
    int Id,
    DateTimeOffset Date,
    string? Text,
    string? Author,
    int Engagement,
    bool IsService);

public sealed record SourceInfo(string Username, SourceType Type);

public sealed record SourceBatch(SourceInfo Source, IReadOnlyList<TgMessage> Messages, int LastPostId);
