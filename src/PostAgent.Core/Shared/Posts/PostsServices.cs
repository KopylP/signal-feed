using Azure.Storage.Queues;
using PostAgent.Shared.Infrastructure;
using PostAgent.Shared.Posts.Storage;

namespace PostAgent.Shared.Posts;

public static class PostsServices
{
    public static IServiceCollection AddPosts(this IServiceCollection services) =>
        services
            .AddSingleton<IPostRepository, PostRepository>()
            .AddKeyedSingleton<IPostQueue>(StorageNames.CollectedQueue,
                (sp, _) => new PostQueue(sp.GetRequiredKeyedService<QueueClient>(StorageNames.CollectedQueue)))
            .AddKeyedSingleton<IPostQueue>(StorageNames.ScoringQueue, (sp, _) => new PostQueue(sp.GetRequiredService<QueueClient>()));
}
