using System.Text.RegularExpressions;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace PostAgent.Shared.Notifications;

// The bot token is part of the request path (/bot{token}/sendMessage), and Application Insights records dependency URLs.
// This initializer replaces the token with *** before the telemetry leaves the app.
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
