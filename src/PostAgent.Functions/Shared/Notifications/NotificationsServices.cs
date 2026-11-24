using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Http.Resilience;
using PostAgent.Shared.Notifications.Telegram;

namespace PostAgent.Shared.Notifications;

public static class NotificationsServices
{
    public static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        services.AddSingleton<ITelemetryInitializer, TelegramTokenRedactor>();
        services.AddSingleton<IChatBindingStore, ChatBindingStore>();
        services.AddSingleton<ChatResolver>();

        // RemoveAllLoggers: токен бота входить в URL запиту, у логи він потрапити не повинен.
        services
            .AddHttpClient<ITelegramNotifier, TelegramNotifier>()
            .RemoveAllLoggers()
            .AddStandardResilienceHandler(ConfigureTelegramResilience);

        return services;
    }

    // Every call to the Bot API is a POST that is not safe to repeat: after a timeout the message may already be delivered, and a repeat sends it again.
    // So POST is not retried; if a send fails, the job fails and the next run repeats the digest.
    // Telegram answered some digest messages slowly (a message arrived after the client had given up), so an attempt gets 30 seconds instead of the default 10.
    public static void ConfigureTelegramResilience(HttpStandardResilienceOptions options)
    {
        options.Retry.DisableForUnsafeHttpMethods();
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60); // must be at least twice the attempt timeout
    }
}
