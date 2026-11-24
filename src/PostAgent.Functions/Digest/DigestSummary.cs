namespace PostAgent.Digest;

public sealed record DigestSummary(string Overview, List<ItemInsight> Items);

public sealed record ItemInsight(string Id, string Insight);
