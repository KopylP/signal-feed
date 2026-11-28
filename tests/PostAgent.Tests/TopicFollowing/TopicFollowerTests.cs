using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using PostAgent.TopicFollowing;

namespace PostAgent.Tests.TopicFollowing;

public class TopicFollowerTests
{
    private readonly InMemoryFollowedTopicStore _topics = new();
    private readonly FakeEmbeddingGenerator _embeddings = new();
    private readonly FakeAlertDecider _alerts = new();

    private TopicFollower Follower(string watches = "google core update;ai search") =>
        new(_topics, _embeddings, _alerts, new FakeEffectiveOptions(new AgentOptions { Watches = watches }), NullLogger<TopicFollower>.Instance);

    [Fact]
    public async Task Embeds_a_new_topic_once_keeps_its_vector_and_has_its_matches_announced()
    {
        _topics.Topics["ai search"] = [1];

        await Follower().FollowAsync("google core update", CancellationToken.None);

        Assert.Equal(["google core update"], Assert.Single(_embeddings.Calls));
        Assert.Equal(["ai search", "google core update"], _topics.Topics.Keys.Order());
        var decision = Assert.Single(_alerts.TopicDecisions);
        Assert.Equal("google core update", decision.Phrase);
        Assert.Equal(512, decision.Vector.Length);
    }

    [Fact]
    public async Task Uses_the_stored_vector_without_a_new_embedding_call()
    {
        _topics.Topics["google core update"] = [0.5f];

        await Follower().FollowAsync("google core update", CancellationToken.None);

        Assert.Empty(_embeddings.Calls);
        Assert.Equal([0.5f], Assert.Single(_alerts.TopicDecisions).Vector);
    }

    [Fact]
    public async Task Does_nothing_for_a_topic_removed_before_it_was_embedded()
    {
        await Follower(watches: "ai search").FollowAsync("google core update", CancellationToken.None);

        Assert.Empty(_embeddings.Calls);
        Assert.Empty(_alerts.TopicDecisions);
    }

    [Fact]
    public async Task Fails_when_the_topic_cannot_be_embedded_so_the_queue_retries_it()
    {
        _embeddings.Failure = new InvalidOperationException("OpenAI is down");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Follower().FollowAsync("google core update", CancellationToken.None));

        Assert.Equal(0, _topics.Saves);
        Assert.Empty(_alerts.TopicDecisions);
    }

    [Fact]
    public async Task The_function_reads_the_phrase_of_the_queue_message()
    {
        await new FollowTopicFunction(Follower()).Run(JsonSerializer.Serialize("ai search"), CancellationToken.None);

        Assert.Equal("ai search", Assert.Single(_alerts.TopicDecisions).Phrase);
    }
}
