using PostAgent.Shared.Stats.Infrastructure;

namespace PostAgent.Shared.Stats;

public static class StatsServices
{
    public static IServiceCollection AddStats(this IServiceCollection services) =>
        services
            .AddSingleton<IDailyStatsStore, BlobDailyStatsStore>()
            .AddSingleton<IUsageStore, BlobUsageStore>();
}
