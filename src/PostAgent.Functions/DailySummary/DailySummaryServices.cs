using PostAgent.Shared.Jobs;

namespace PostAgent.DailySummary;

public static class DailySummaryServices
{
    public static IServiceCollection AddDailySummary(this IServiceCollection services) =>
        services.AddJob<DailySummaryJob>();
}
