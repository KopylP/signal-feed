using PostAgent.Collecting.Storage;
using PostAgent.Collecting.Telegram;

namespace PostAgent.Collecting;

public static class CollectingServices
{
    public static IServiceCollection AddCollecting(this IServiceCollection services) =>
        services
            .AddSingleton<ISourceCursors, SourceCursors>()
            .AddScoped<ISourceReader, TelegramSourceReader>();
}
