namespace PostAgent.Shared.Stats;

// Tokens one purpose used on one UTC day. Purpose is "scoring" (also the digest, they share a client) or "chat".
public sealed record UsageDay(string Day, string Purpose, int Calls, long InputTokens, long OutputTokens);
