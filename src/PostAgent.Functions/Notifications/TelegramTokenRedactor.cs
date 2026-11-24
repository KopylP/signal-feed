using System.Text.RegularExpressions;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace PostAgent.Notifications;

// Токен бота входить у шлях запиту (/bot{token}/sendMessage), а Application Insights записує URL залежностей.
// Цей ініціалізатор замінює токен на *** до того, як телеметрія піде з застосунку.
public sealed partial class TelegramTokenRedactor : ITelemetryInitializer
{
    private const string TelegramHost = "api.telegram.org";

    public void Initialize(ITelemetry telemetry)
    {
        if (telemetry is DependencyTelemetry dependency && dependency.Target?.Contains(TelegramHost) == true)
        {
            dependency.Data = Redact(dependency.Data);
            dependency.Name = Redact(dependency.Name);
        }
    }

    public static string? Redact(string? text) => text is null ? null : BotPath().Replace(text, "/bot***/");

    [GeneratedRegex("/bot[^/]+/")]
    private static partial Regex BotPath();
}
