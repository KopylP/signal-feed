using PostAgent.Deduplication.Infrastructure;

namespace PostAgent.Deduplication;

public static class DeduplicationServices
{
    public static IServiceCollection AddDeduplication(this IServiceCollection services) =>
        services
            .AddSingleton<IDeduplicationLock, BlobDeduplicationLock>()
            .AddSingleton<PostDeduplicator>();
}
