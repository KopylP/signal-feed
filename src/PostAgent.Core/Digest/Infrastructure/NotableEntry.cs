namespace PostAgent.Digest.Infrastructure;

// A separate type from NoteEntry on purpose: a type used twice in the schema becomes a "$ref" that OpenAI strict mode rejects.
public sealed record NotableEntry(string Id, string Title, string Note);
