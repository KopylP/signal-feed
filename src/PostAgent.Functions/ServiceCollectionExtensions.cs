using Microsoft.Extensions.Options;
using PostAgent.Alerts;
using PostAgent.Chat;
using PostAgent.Cleanup;
using PostAgent.Collecting;
using PostAgent.Digest;
using PostAgent.Feedback;
using PostAgent.Login;
using PostAgent.Pairing;
using PostAgent.Scoring;
using PostAgent.SettingsMenu;
using PostAgent.Shared.Ai;
using PostAgent.Shared.Infrastructure;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;
using PostAgent.Shared.Stats;
using PostAgent.Shared.TelegramSession;
using PostAgent.Stats;
using PostAgent.Webhook;

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
            .AddSessionCipher(config)
            .AddSettings()
            .AddStats()
            .AddAi()
            .AddPosts()
            .AddNotifications()
            .AddWebhook()
            .AddPairing()
            .AddLogin()
            .AddFeedback()
            .AddStatsCommand()
            .AddSettingsMenu()
            .AddChat()
            .AddCollecting()
            .AddScoring()
            .AddDigest()
            .AddCleanup()
            .AddAlerts();
    }
}
