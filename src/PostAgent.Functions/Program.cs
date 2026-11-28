using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using PostAgent;
using PostAgent.Shared.Infrastructure;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights()
    .AddPostAgent(builder.Configuration);

// By default Application Insights drops logs below Warning. We remove that rule
// so the levels from host.json apply.
builder.Logging.Services.Configure<LoggerFilterOptions>(filter =>
{
    var rule = filter.Rules.FirstOrDefault(r =>
        r.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
    if (rule is not null)
    {
        filter.Rules.Remove(rule);
    }
});

// WTelegramClient logs every connection and session event at Information; only its warnings and errors are worth the log quota.
builder.Logging.AddFilter("WTelegram", LogLevel.Warning);

var app = builder.Build();

// WTelegramClient logs (a static hook of the library) go to the common ILogger.
var wtelegramLog = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("WTelegram");
WTelegram.Helpers.Log = (level, text) => wtelegramLog.Log((LogLevel)level, "{Text}", text);

await app.Services.EnsureLocalResourcesAsync(app.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);
await app.RunAsync();
