using PostAgent.Collecting.Infrastructure;
using PostAgent.Collecting.Infrastructure.Telegram;
using PostAgent.Shared.Jobs;

namespace PostAgent.Collecting;

public static class CollectingServices
{
    public static IServiceCollection AddCollecting(this IServiceCollection services) =>
        services
            .AddSingleton<ISourceCursors, SourceCursors>()
            .AddSingleton<ICollectionLock, BlobCollectionLock>()
            .AddScoped<ISourceReader, TelegramSourceReader>()
            .AddScoped<SourceCollector>()
            .AddJob<CollectorJob>();
}
