namespace PostAgent.TopicFollowing;

public static class TopicFollowingServices
{
    public static IServiceCollection AddTopicFollowing(this IServiceCollection services) =>
        services.AddSingleton<TopicFollower>();
}
