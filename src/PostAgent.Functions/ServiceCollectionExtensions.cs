using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace PostAgent;

public static class ServiceCollectionExtensions
{
    /// <summary>Усі сервіси застосунку. Окремий метод, щоб тест міг зібрати той самий граф залежностей, що й Program.cs.</summary>
    public static IServiceCollection AddPostAgent(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<AgentOptions>()
            .Bind(config)
            .ValidateDataAnnotations()
            .ValidateOnStart(); // помилка в налаштуваннях зупиняє запуск зрозумілим повідомленням
        services.AddSingleton<IValidateOptions<AgentOptions>, AgentOptionsValidator>();
        services.AddSingleton(TimeProvider.System);
        services.AddStorage(config);

        services.AddSingleton<PostRepository>();
        services.AddSingleton<SourceCursors>();
        services.AddSingleton<ScoringQueue>();
        services.AddSingleton<ICleanupTask, PostsCleanup>();

        services.AddChatClient(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
            return new OpenAI.Chat.ChatClient(settings.OpenAIModel, settings.OpenAIApiKey).AsIChatClient();
        });
        services.AddSingleton<PostScorer>();
        services.AddSingleton<DigestSummarizer>();

        services.AddScoped<TelegramSourceReader>();

        services.AddSingleton<ITelemetryInitializer, TelegramTokenRedactor>();
        // RemoveAllLoggers: токен бота входить в URL запиту, у логи він потрапити не повинен.
        services
            .AddHttpClient<TelegramNotifier>()
            .RemoveAllLoggers()
            // Повтори, таймаути й circuit breaker. Повтор POST sendMessage після таймауту інколи дає дубль
            // повідомлення, але це краще за втрачений дайджест.
            .AddStandardResilienceHandler();

        return services;
    }
}
