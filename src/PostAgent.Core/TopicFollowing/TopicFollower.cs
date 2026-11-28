using Microsoft.Extensions.AI;
using PostAgent.Shared.Ai;
using PostAgent.Shared.Alerts;
using PostAgent.Shared.Settings;
using PostAgent.Shared.Topics;

namespace PostAgent.TopicFollowing;

// Starts following a topic that was just added (ADR-0024): embeds it once, keeps its vector for every alert decision, and has its
// matches among the recent posts announced. The only place that embeds topics.
public sealed class TopicFollower(
    IFollowedTopicStore topics,
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
    IAlertDecider alerts,
    IEffectiveOptions options,
    ILogger<TopicFollower> log)
{
    public async Task FollowAsync(string phrase, CancellationToken ct)
    {
        // Fresh, not the cached copy: the topic was added on another instance a moment ago.
        var followed = (await options.GetFreshAsync(ct)).WatchList;
        if (!followed.Contains(phrase, StringComparer.OrdinalIgnoreCase))
        {
            log.LogInformation("A topic was removed before it was embedded, nothing to do");
            return;
        }

        var stored = await topics.LoadAsync(ct);
        if (!stored.TryGetValue(phrase, out var vector))
        {
            vector = (await embeddings.EmbedAsync([phrase], ct))[0];
            await topics.SaveAsync(phrase, vector, ct);
        }

        await alerts.DecideTopicAsync(phrase, vector, ct);
    }
}
