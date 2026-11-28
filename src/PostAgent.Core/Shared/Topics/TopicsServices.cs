using PostAgent.Shared.Topics.Infrastructure;

namespace PostAgent.Shared.Topics;

public static class TopicsServices
{
    public static IServiceCollection AddTopics(this IServiceCollection services) =>
        services
            .AddSingleton<IFollowedTopicStore, BlobFollowedTopicStore>()
            .AddSingleton<ITopicsToFollowQueue, TopicsToFollowQueue>();
}
