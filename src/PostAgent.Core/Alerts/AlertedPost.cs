namespace PostAgent.Alerts;

// One post of an alert; Topic is the followed topic it matched (Topic alerts only).
public sealed record AlertedPost(string PartitionKey, string RowKey, string? Topic = null);
