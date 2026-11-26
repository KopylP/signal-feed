using PostAgent.Shared.Cleanup;
using PostAgent.Shared.Cleanup.Infrastructure;

namespace PostAgent.Cleanup;

public static class CleanupServices
{
    // A new cleanup: add a class that implements ICleanupTask and register it here.
    public static IServiceCollection AddCleanup(this IServiceCollection services) =>
        services
            .AddSingleton<ICleanupTask, PostsCleanup>()
            .AddSingleton<ICleanupTask, ChatStateCleanup>()
            .AddSingleton<ICleanupTask, OldDailySummariesCleanup>()
            .AddSingleton<ICleanupRequestQueue, CleanupRequestQueue>()
            .AddSingleton<CleanupRunner>();
}
