using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using PostAgent;
using PostAgent.Infrastructure;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights()
    .AddPostAgent(builder.Configuration);

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

var app = builder.Build();

// Логи WTelegramClient (статичний хук бібліотеки) йдуть у загальний ILogger.
var wtelegramLog = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("WTelegram");
WTelegram.Helpers.Log = (level, text) => wtelegramLog.Log((LogLevel)level, "{Text}", text);

await app.Services.EnsureLocalResourcesAsync();
await app.RunAsync();
