using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PostAgent.Alerts;
using PostAgent.Cleanup;
using PostAgent.Collecting;
using PostAgent.Digest;
using PostAgent.Scoring;
using PostAgent.Settings;

namespace PostAgent.Tests;

public class CompositionTests
{
    private static readonly Dictionary<string, string?> ValidSettings = new()
    {
        ["AzureWebJobsStorage"] = "UseDevelopmentStorage=true",
        ["Sources"] = "News, @chat",
        ["AlertSources"] = "chat",
        ["OpenAIApiKey"] = "key",
        ["TelegramBotToken"] = "123456:ABC-DEF",
        ["TelegramChatId"] = "-1001234567890",
        ["TelegramApiId"] = "12345",
        ["TelegramApiHash"] = "hash",
        ["TelegramPhone"] = "+380000000000",
        ["DigestSize"] = "6",
    };

    private static ServiceProvider BuildProvider(Action<Dictionary<string, string?>>? change = null)
    {
        var settings = new Dictionary<string, string?>(ValidSettings);
        change?.Invoke(settings);
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        return new ServiceCollection()
            .AddLogging()
            .AddPostAgent(config)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    [Fact]
    public void Settings_are_bound_from_configuration()
    {
        using var provider = BuildProvider();

        var options = provider.GetRequiredService<IOptions<AgentOptions>>().Value;

        Assert.Equal(["news", "chat"], options.SourceList);
        Assert.Equal(["chat"], options.AlertSourceSet);
        Assert.Equal(-1001234567890, options.TelegramChatId);
        Assert.Equal(6, options.DigestSize);
        Assert.Equal(5, options.MaxItemsPerGroup); // значення за замовчуванням із коду
    }

    [Fact]
    public void Every_function_can_be_constructed_from_the_container()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;

        Assert.NotNull(ActivatorUtilities.CreateInstance<CollectorFunction>(services));
        Assert.NotNull(ActivatorUtilities.CreateInstance<ScorerFunction>(services));
        Assert.NotNull(ActivatorUtilities.CreateInstance<AlertFunction>(services));
        Assert.NotNull(ActivatorUtilities.CreateInstance<DigestFunction>(services));
        Assert.NotNull(ActivatorUtilities.CreateInstance<CleanupFunction>(services));
    }

    [Fact]
    public void Cleanup_runs_every_registered_task()
    {
        using var provider = BuildProvider();

        Assert.Contains(provider.GetServices<ICleanupTask>(), task => task is PostsCleanup);
    }

    [Fact]
    public void Invalid_settings_stop_the_application_with_the_name_of_the_setting()
    {
        using var provider = BuildProvider(s => s["OpenAIApiKey"] = "");

        var error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<AgentOptions>>().Value);

        Assert.Contains("OpenAIApiKey", error.Message);
    }

    [Fact]
    public void Every_setting_in_local_settings_example_is_known_to_the_code()
    {
        var path = Path.Combine(RepoRoot(), "src", "PostAgent.Functions", "local.settings.json.example");
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var known = typeof(AgentOptions).GetProperties().Select(p => p.Name)
            .Concat(["AzureWebJobsStorage", "FUNCTIONS_WORKER_RUNTIME"])
            .ToHashSet();

        var unknown = json.RootElement.GetProperty("Values").EnumerateObject()
            .Select(p => p.Name)
            .Where(name => !known.Contains(name) && !name.EndsWith("Schedule", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(unknown);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SignalFeed.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
