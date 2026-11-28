namespace PostAgent.Shared.Topics;

// The vectors of the followed topics (settings/followed-topics.json, ADR-0024): written once per new topic by FollowTopicFunction,
// read by every alert decision. A topic without a vector matches by words only.
public interface IFollowedTopicStore
{
    // Phrase (any case) to its vector; empty when nothing is stored.
    Task<IReadOnlyDictionary<string, float[]>> LoadAsync(CancellationToken ct);

    // Adds or replaces the vector of one phrase; the others stay (a vector of a topic no longer followed is never used). Retries a lost race.
    Task SaveAsync(string phrase, float[] vector, CancellationToken ct);
}
