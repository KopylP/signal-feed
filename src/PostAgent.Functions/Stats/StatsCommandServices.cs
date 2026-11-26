using PostAgent.Shared.Notifications;

namespace PostAgent.Stats;

public static class StatsCommandServices
{
    public static IServiceCollection AddStatsCommand(this IServiceCollection services) =>
        services.AddScoped<IIncomingMessageHandler, StatsInbox>();
}
