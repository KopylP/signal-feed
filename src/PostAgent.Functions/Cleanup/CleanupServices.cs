using PostAgent.Shared.Jobs;

namespace PostAgent.Cleanup;

public static class CleanupServices
{
    // Нове очищення: додайте клас, що реалізує ICleanupTask, і зареєструйте його тут.
    public static IServiceCollection AddCleanup(this IServiceCollection services) =>
        services
            .AddSingleton<ICleanupTask, DailyStatsTask>() // before PostsCleanup: a day is summarized, then deleted
            .AddSingleton<ICleanupTask, PostsCleanup>()
            .AddSingleton<ICleanupTask, ChatStateCleanup>()
            .AddJob<CleanupJob>();
}
