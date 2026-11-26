namespace PostAgent.Shared.Stats;

public sealed record TopPost(string PostId, string Source, string? Topic, int Score, string Url);
