using PostAgent.Collecting.Infrastructure;
using PostAgent.Collecting.Infrastructure.Telegram;
using PostAgent.Shared.Jobs;
using PostAgent.Shared.TelegramSession;

namespace PostAgent.Collecting;

public static class CollectingServices
{
    public static IServiceCollection AddCollecting(this IServiceCollection services) =>
        services
            .AddSingleton<ISourceCursors, SourceCursors>()
            .AddSingleton<ISessionLock, BlobSessionLock>()
            .AddScoped<ISourceReader, TelegramSourceReader>()
            .AddScoped<SourceCollector>()
            .AddJob<CollectorJob>();
}
