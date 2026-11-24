namespace PostAgent.Collecting;

public sealed record SourceBatch(SourceInfo Source, IReadOnlyList<TgMessage> Messages, int LastPostId);
