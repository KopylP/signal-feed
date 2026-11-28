using Microsoft.Extensions.Options;
using PostAgent.Alerts;
using PostAgent.Chat;
using PostAgent.Cleanup;
using PostAgent.Collecting;
using PostAgent.DailySummary;
using PostAgent.Deduplication;
using PostAgent.Digest;
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
using PostAgent.Shared.Topics;
using PostAgent.Stats;
using PostAgent.TopicFollowing;
using PostAgent.Webhook;

namespace PostAgent;

public static class ServiceCollectionExtensions
{
    // A separate method, so a test can build the same dependency graph as Program.cs.
    public static IServiceCollection AddPostAgent(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<AgentOptions>()
            .Bind(config)
            .ValidateDataAnnotations()
            .ValidateOnStart(); // a settings error stops the start with a clear message
        services.AddSingleton<IValidateOptions<AgentOptions>, AgentOptionsValidator>();
        services.AddSingleton(TimeProvider.System);

        return services
            .AddStorage(config)
            .AddSessionCipher(config)
            .AddSettings()
            .AddStatsStores()
            .AddAi()
            .AddPosts()
            .AddTopics()
            .AddNotifications()
            .AddWebhook()
            .AddPairing()
            .AddLogin()
            .AddStatsCommand()
            .AddSettingsMenu()
            .AddChat()
            .AddCollecting()
            .AddDeduplication()
            .AddScoring()
            .AddDigest()
            .AddCleanup()
            .AddDailySummary()
            .AddAlerts()
            .AddTopicFollowing();
    }
}
