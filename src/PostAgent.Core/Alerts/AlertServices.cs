using PostAgent.Shared.Jobs;

namespace PostAgent.Alerts;

public static class AlertServices
{
    public static IServiceCollection AddAlerts(this IServiceCollection services) =>
        services.AddJob<AlertJob>();
}
