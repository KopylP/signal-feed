using Microsoft.ApplicationInsights.Extensibility;
using PostAgent.Shared.Notifications.Telegram;

namespace PostAgent.Shared.Notifications;

public static class NotificationsServices
{
    public static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        services.AddSingleton<ITelemetryInitializer, TelegramTokenRedactor>();

        // RemoveAllLoggers: токен бота входить в URL запиту, у логи він потрапити не повинен.
        services
            .AddHttpClient<ITelegramNotifier, TelegramNotifier>()
            .RemoveAllLoggers()
            // Повтори, таймаути й circuit breaker. Повтор POST sendMessage після таймауту інколи дає дубль
            // повідомлення, але це краще за втрачений дайджест.
            .AddStandardResilienceHandler();

        return services;
    }
}
