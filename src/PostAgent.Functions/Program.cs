using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostAgent;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// За замовчуванням Application Insights пропускає логи нижче Warning. Прибираємо це правило,
// щоб діяли рівні з host.json.
builder.Logging.Services.Configure<LoggerFilterOptions>(filter =>
{
    var rule = filter.Rules.FirstOrDefault(r =>
        r.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
    if (rule is not null)
    {
        filter.Rules.Remove(rule);
    }
});

builder.Services.AddOptions<AgentOptions>()
    .Bind(builder.Configuration)
    .ValidateDataAnnotations()
    .ValidateOnStart(); // помилка в налаштуваннях зупиняє запуск зрозумілим повідомленням
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddStorage(builder.Configuration);

builder.Services.AddSingleton<IValidateOptions<AgentOptions>, AgentOptionsValidator>();
builder.Services.AddSingleton<PostRepository>();
builder.Services.AddSingleton<SourceCursors>();
builder.Services.AddSingleton<ScoringQueue>();
builder.Services.AddChatClient(sp =>
{
    var settings = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
    return new OpenAI.Chat.ChatClient(settings.OpenAIModel, settings.OpenAIApiKey).AsIChatClient();
});
builder.Services.AddSingleton<PostScorer>();
builder.Services.AddSingleton<DigestSummarizer>();
builder.Services.AddSingleton<ICleanupTask, PostsCleanup>();
builder.Services.AddScoped<TelegramSourceReader>();

// RemoveAllLoggers: токен бота входить в URL запиту, у логи він потрапити не повинен.
builder.Services
    .AddHttpClient<TelegramNotifier>()
    .RemoveAllLoggers()
    // Повтори, таймаути й circuit breaker. Повтор POST sendMessage після таймауту інколи дає дубль повідомлення,
    // але це краще за втрачений дайджест.
    .AddStandardResilienceHandler();

var app = builder.Build();

// Логи WTelegramClient (статичний хук бібліотеки) йдуть у загальний ILogger.
var wtelegramLog = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("WTelegram");
WTelegram.Helpers.Log = (level, text) => wtelegramLog.Log((LogLevel)level, "{Text}", text);

await app.Services.EnsureLocalResourcesAsync();
await app.RunAsync();
