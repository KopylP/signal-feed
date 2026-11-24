namespace PostAgent.Collecting;

public sealed record TgMessage(
    int Id,
    DateTimeOffset Date,
    string? Text,
    string? Author,
    int Engagement,
    bool IsService);
