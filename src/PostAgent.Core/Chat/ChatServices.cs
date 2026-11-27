using Microsoft.Extensions.Http.Resilience;
using PostAgent.Chat.Agent;
using PostAgent.Chat.Agent.Tools;
using PostAgent.Chat.Answering;
using PostAgent.Chat.Infrastructure;
using PostAgent.Chat.Intake;
using PostAgent.Chat.Memory;
using PostAgent.Shared.Chat;
using PostAgent.Shared.Notifications;

namespace PostAgent.Chat;

public static class ChatServices
{
    public static IServiceCollection AddChat(this IServiceCollection services) =>
        services
            .AddSingleton<IChatStateStore, BlobChatStateStore>()
            .AddSingleton<IReaderMemoryStore, BlobReaderMemoryStore>()
            .AddSingleton<ReaderMemory>()
            .AddChatTools()
            .AddChatDrafts()
            .AddSingleton<ChatToolFactory>()
            .AddScoped<IChatAgent, ChatAgent>()
            .AddSingleton<IChatQueue, ChatQueue>()
            .AddScoped<IIncomingMessageHandler, ClearInbox>()
            .AddScoped<IIncomingMessageHandler, ChatInbox>()
            .AddScoped<SettingsProposalSender>()
            .AddScoped<ChatDraftStreams>()
            .AddScoped<ChatDelivery>()
            .AddScoped<ChatProcessor>();

    // Like every Bot API call, a draft is a POST that is not repeated.
    public static IServiceCollection AddChatDrafts(this IServiceCollection services)
    {
        services
            .AddHttpClient<IChatDrafts, TelegramChatDrafts>()
            .RemoveAllLoggers()
            .AddStandardResilienceHandler(ConfigureDraftResilience);
        return services;
    }

    // A draft is awaited inside the model loop, so it gets a short budget and no retry.
    public static void ConfigureDraftResilience(HttpStandardResilienceOptions options)
    {
        options.Retry.DisableForUnsafeHttpMethods();
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(8);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10); // at least twice the attempt timeout
    }

    // Every IChatTool in the assembly is a tool of the agent: a new tool is a new file, with nothing to register by hand.
    public static IServiceCollection AddChatTools(this IServiceCollection services)
    {
        var tools = typeof(IChatTool).Assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && t.IsAssignableTo(typeof(IChatTool)));
        foreach (var tool in tools)
        {
            services.AddSingleton(typeof(IChatTool), tool);
        }

        return services;
    }
}
