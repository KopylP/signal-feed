using Microsoft.ApplicationInsights.DataContracts;

namespace PostAgent.Tests;

public class TelegramTokenRedactorTests
{
    private const string Token = "123456:ABC-DEF_secret";

    [Fact]
    public void Bot_token_is_removed_from_telegram_dependency_telemetry()
    {
        var dependency = new DependencyTelemetry
        {
            Type = "Http",
            Target = "api.telegram.org",
            Name = $"POST /bot{Token}/sendMessage",
            Data = $"https://api.telegram.org/bot{Token}/sendMessage",
        };

        new TelegramTokenRedactor().Initialize(dependency);

        Assert.Equal("POST /bot***/sendMessage", dependency.Name);
        Assert.Equal("https://api.telegram.org/bot***/sendMessage", dependency.Data);
        Assert.DoesNotContain("secret", dependency.Name + dependency.Data);
    }

    [Fact]
    public void Other_dependencies_are_left_untouched()
    {
        var dependency = new DependencyTelemetry { Target = "api.openai.com", Name = "POST /v1/chat/completions", Data = "https://api.openai.com/v1/chat/completions" };

        new TelegramTokenRedactor().Initialize(dependency);

        Assert.Equal("https://api.openai.com/v1/chat/completions", dependency.Data);
    }

    [Fact]
    public void Missing_name_or_data_does_not_throw()
    {
        var dependency = new DependencyTelemetry { Target = "api.telegram.org" };

        new TelegramTokenRedactor().Initialize(dependency);

        Assert.Null(TelegramTokenRedactor.Redact(null));
    }
}
