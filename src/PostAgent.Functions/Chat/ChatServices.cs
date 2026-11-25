using PostAgent.Chat.Agent;
using PostAgent.Chat.Agent.Tools;
using PostAgent.Chat.Answering;
using PostAgent.Chat.Infrastructure;
using PostAgent.Chat.Intake;
using PostAgent.Shared.Chat;
using PostAgent.Shared.Notifications;

namespace PostAgent.Chat;

public static class ChatServices
{
    public static IServiceCollection AddChat(this IServiceCollection services) =>
        services
            .AddSingleton<IChatStateStore, BlobChatStateStore>()
            .AddChatTools()
            .AddSingleton<ChatToolFactory>()
            .AddSingleton<IChatAgent, ChatAgent>()
            .AddSingleton<IChatQueue, ChatQueue>()
            .AddScoped<IIncomingMessageHandler, ChatInbox>()
            .AddScoped<SettingsProposalSender>()
            .AddScoped<ChatDelivery>()
            .AddScoped<ChatProcessor>();

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
