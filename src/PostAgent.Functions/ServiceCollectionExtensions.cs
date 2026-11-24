using Microsoft.Extensions.Options;
using PostAgent.Ai;
using PostAgent.Cleanup;
using PostAgent.Collecting;
using PostAgent.Digest;
using PostAgent.Infrastructure;
using PostAgent.Notifications;
using PostAgent.Posts;
using PostAgent.Scoring;
using PostAgent.Settings;

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
            .AddAi()
            .AddPosts()
            .AddNotifications()
            .AddCollecting()
            .AddScoring()
            .AddDigest()
            .AddCleanup();
    }
}
