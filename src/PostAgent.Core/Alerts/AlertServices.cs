using PostAgent.Alerts.Infrastructure;
using PostAgent.Shared.Alerts;

namespace PostAgent.Alerts;

public static class AlertServices
{
    public static IServiceCollection AddAlerts(this IServiceCollection services) =>
        services
            .AddSingleton<IAlertDecisionLock, BlobAlertDecisionLock>()
            .AddSingleton<IAlertQueue, AlertQueue>()
            .AddSingleton<IAlertDecider, AlertDecider>()
            .AddSingleton<AlertSender>();
}
