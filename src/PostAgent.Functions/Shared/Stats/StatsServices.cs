using PostAgent.Shared.Stats.Infrastructure;

namespace PostAgent.Shared.Stats;

public static class StatsServices
{
    public static IServiceCollection AddStatsStores(this IServiceCollection services) =>
        services
            .AddSingleton<IDailyStatsStore, BlobDailyStatsStore>()
            .AddSingleton<IUsageStore, BlobUsageStore>();
}
