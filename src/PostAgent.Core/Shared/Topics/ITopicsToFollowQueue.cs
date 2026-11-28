namespace PostAgent.Shared.Topics;

// A topic that just started to be followed, on its way to FollowTopicFunction. Only a phrase new to the list is published.
public interface ITopicsToFollowQueue
{
    Task EnqueueAsync(string phrase, CancellationToken ct);
}
