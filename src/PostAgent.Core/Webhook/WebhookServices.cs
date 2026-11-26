namespace PostAgent.Webhook;

public static class WebhookServices
{
    public static IServiceCollection AddWebhook(this IServiceCollection services) =>
        services.AddScoped<TelegramWebhook>();
}
