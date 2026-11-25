using System.Text.Json;
using Azure.Storage.Queues;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PostAgent.Alerts;
using PostAgent.Chat;
using PostAgent.Chat.Agent;
using PostAgent.Cleanup;
using PostAgent.Collecting;
using PostAgent.Digest;
using PostAgent.Pairing;
using PostAgent.RunNow;
using PostAgent.Scoring;
using PostAgent.Shared.Ai;
using PostAgent.Shared.Infrastructure;
using PostAgent.Shared.Jobs;
using PostAgent.Shared.Settings;

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
        ["TelegramPairingKey"] = "pairing-key-0123456789",
        ["TelegramWebhookSecret"] = "webhook_secret-token",
        ["TelegramApiId"] = "12345",
        ["TelegramApiHash"] = "hash",
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
        Assert.NotNull(ActivatorUtilities.CreateInstance<PairingFunction>(services));
        Assert.NotNull(ActivatorUtilities.CreateInstance<RunNowFunction>(services));
        Assert.NotNull(ActivatorUtilities.CreateInstance<ChatFunction>(services));
        Assert.NotNull(ActivatorUtilities.CreateInstance<PostAgent.Chat.Infrastructure.ChatQueue>(services));
        Assert.NotNull(ActivatorUtilities.CreateInstance<PostAgent.Login.LoginFunction>(services));
        Assert.NotNull(ActivatorUtilities.CreateInstance<PostAgent.Login.LoginPageFunction>(services));
        Assert.NotNull(ActivatorUtilities.CreateInstance<PostAgent.Login.Infrastructure.LoginQueue>(services));
    }

    [Fact]
    public void Keeps_the_session_unencrypted_only_for_local_storage()
    {
        using var provider = BuildProvider();

        Assert.IsType<PostAgent.Shared.TelegramSession.UnencryptedLocalCipher>(provider.GetRequiredService<PostAgent.Shared.TelegramSession.ISessionCipher>());
    }

    [Fact]
    public void Encrypts_the_session_with_the_key_vault_key_when_it_is_set()
    {
        using var provider = BuildProvider(s => s["SessionKeyId"] = "https://kv-signal.vault.azure.net/keys/tg-session");

        Assert.IsType<PostAgent.Shared.TelegramSession.EnvelopeCipher>(provider.GetRequiredService<PostAgent.Shared.TelegramSession.ISessionCipher>());
    }

    [Fact]
    public void Refuses_to_start_in_azure_without_the_session_key()
    {
        var error = Assert.Throws<InvalidOperationException>(() => BuildProvider(s =>
        {
            s.Remove("AzureWebJobsStorage");
            s["AzureWebJobsStorage:accountName"] = "stsignal";
        }));

        Assert.Contains("SessionKeyId", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_host_name_of_the_mini_app_comes_from_the_platform_setting()
    {
        using var provider = BuildProvider(s => s["WEBSITE_HOSTNAME"] = "func-signal.azurewebsites.net");

        Assert.Equal("func-signal.azurewebsites.net", provider.GetRequiredService<IOptions<AgentOptions>>().Value.WebsiteHostName);
    }

    [Fact]
    public void The_chat_has_its_own_model_client_next_to_the_scoring_one()
    {
        using var provider = BuildProvider();

        var scoring = provider.GetRequiredService<IChatClient>();
        var chat = provider.GetRequiredKeyedService<IChatClient>(AiServices.ChatClientKey);

        Assert.NotSame(scoring, chat);
        Assert.Equal("gpt-5.4-nano", scoring.GetService<ChatClientMetadata>()?.DefaultModelId);
        Assert.Equal("gpt-6.1-terra", chat.GetService<ChatClientMetadata>()?.DefaultModelId);
    }

    [Fact]
    public void The_chat_model_follows_the_setting()
    {
        using var provider = BuildProvider(s => s["OpenAIChatModel"] = "gpt-6.1-sol");

        var chat = provider.GetRequiredKeyedService<IChatClient>(AiServices.ChatClientKey);

        Assert.Equal("gpt-6.1-sol", chat.GetService<ChatClientMetadata>()?.DefaultModelId);
    }

    [Fact]
    public void The_chat_queue_is_a_separate_keyed_client_and_the_scoring_queue_stays_unkeyed()
    {
        using var provider = BuildProvider();

        var scoring = provider.GetRequiredService<QueueClient>();
        var chat = provider.GetRequiredKeyedService<QueueClient>(StorageNames.ChatQueue);

        Assert.Equal(StorageNames.ScoringQueue, scoring.Name);
        Assert.Equal(StorageNames.ChatQueue, chat.Name);
    }

    [Fact]
    public void The_chat_agent_is_built_from_the_container_with_the_keyed_chat_client()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        // Scoped: its Telegram client is a typed HttpClient, which must not live as long as the process.
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IChatAgent>());
    }

    [Fact]
    public void Chat_settings_have_their_defaults()
    {
        using var provider = BuildProvider();

        var options = provider.GetRequiredService<IOptions<AgentOptions>>().Value;

        Assert.Equal("gpt-6.1-terra", options.OpenAIChatModel);
        Assert.Equal(20, options.ChatMaxMessages);
        Assert.Equal(4000, options.ChatMaxOutputTokens);
        Assert.Equal("medium", options.ChatReasoningEffort);
        Assert.Equal(5, options.ChatMaxToolCalls);
        Assert.Equal(8, options.ChatSearchMaxHits);
    }

    [Fact]
    public void A_chat_setting_out_of_range_stops_the_application_with_its_name()
    {
        using var provider = BuildProvider(s => s["ChatMaxToolCalls"] = "0");

        var error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<AgentOptions>>().Value);

        Assert.Contains("ChatMaxToolCalls", error.Message);
    }

    [Fact]
    public void Every_timer_job_is_registered_for_run_now_under_a_unique_name()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var names = scope.ServiceProvider.GetServices<IJob>().Select(j => j.Name).Order().ToList();

        Assert.Equal(["alert", "cleanup", "collect", "digest"], names);
    }

    [Fact]
    public void The_run_now_command_line_knows_every_job_and_the_queue_name()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var tool = File.ReadAllText(Path.Combine(RepoRoot(), "tools", "run-now.cs"));

        var jobsLine = tool.Split('\n').Single(l => l.StartsWith("string[] jobs = ", StringComparison.Ordinal));
        Assert.Contains($"const string queueName = \"{StorageNames.RunNowQueue}\";", tool);
        Assert.All(scope.ServiceProvider.GetServices<IJob>(), job => Assert.Contains($"\"{job.Name}\"", jobsLine));
    }

    [Fact]
    public void Cleanup_runs_every_registered_task()
    {
        using var provider = BuildProvider();

        Assert.Contains(provider.GetServices<ICleanupTask>(), task => task is PostsCleanup);
        Assert.Contains(provider.GetServices<ICleanupTask>(), task => task is ChatStateCleanup);
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
