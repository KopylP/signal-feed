namespace PostAgent.Shared.Settings;

// Verified is false until the collector has read the source once without an error.
public sealed record SourceEntry(string Name, bool Verified);
