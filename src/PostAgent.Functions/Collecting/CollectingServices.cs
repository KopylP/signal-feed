using PostAgent.Collecting.Infrastructure;
using PostAgent.Collecting.Infrastructure.Telegram;

namespace PostAgent.Collecting;

public static class CollectingServices
{
    public static IServiceCollection AddCollecting(this IServiceCollection services) =>
        services
            .AddSingleton<ISourceCursors, SourceCursors>()
            .AddScoped<ISourceReader, TelegramSourceReader>();
}
