using Microsoft.Azure.Functions.Worker;
using PostAgent.Shared.Infrastructure;
using PostAgent.Shared.Topics.Infrastructure;

namespace PostAgent.TopicFollowing;

public sealed class FollowTopicFunction(TopicFollower follower)
{
    [Function(nameof(FollowTopicFunction))]
    public Task Run([QueueTrigger(StorageNames.TopicsToFollowQueue, Connection = "AzureWebJobsStorage")] string message, CancellationToken ct) =>
        follower.FollowAsync(TopicsToFollowQueue.Parse(message), ct);
}
