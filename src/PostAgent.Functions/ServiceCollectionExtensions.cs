using Microsoft.Extensions.Options;
using PostAgent.Alerts;
using PostAgent.Chat;
using PostAgent.Cleanup;
using PostAgent.Collecting;
using PostAgent.Digest;
using PostAgent.Pairing;
using PostAgent.Scoring;
using PostAgent.Shared.Ai;
using PostAgent.Shared.Infrastructure;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;

namespace PostAgent;

public static class ServiceCollectionExtensions
{
    // Окремий метод, щоб тест міг зібрати той самий граф залежностей, що й Program.cs.
    public static IServiceCollection AddPostAgent(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<AgentOptions>()
            .Bind(config)
            .ValidateDataAnnotations()
            .ValidateOnStart(); // помилка в налаштуваннях зупиняє запуск зрозумілим повідомленням
        services.AddSingleton<IValidateOptions<AgentOptions>, AgentOptionsValidator>();
        services.AddSingleton(TimeProvider.System);

        return services
            .AddStorage(config)
            .AddSettings()
            .AddAi()
            .AddPosts()
            .AddNotifications()
            .AddPairing()
            .AddChat()
            .AddCollecting()
            .AddScoring()
            .AddDigest()
            .AddCleanup()
            .AddAlerts();
    }
}
