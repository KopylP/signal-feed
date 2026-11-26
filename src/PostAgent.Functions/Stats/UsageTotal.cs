namespace PostAgent.Stats;

public sealed record UsageTotal(string Purpose, int Calls, long InputTokens, long OutputTokens);
