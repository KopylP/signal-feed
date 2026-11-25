using PostAgent.Chat.Infrastructure;
using PostAgent.Shared.Chat;
using PostAgent.Shared.Notifications;

namespace PostAgent.Chat;

public static class ChatServices
{
    public static IServiceCollection AddChat(this IServiceCollection services) =>
        services
            .AddSingleton<IChatStateStore, BlobChatStateStore>()
            .AddSingleton<ChatTools>()
            .AddSingleton<SettingsToolsFactory>()
            .AddSingleton<IChatAgent, ChatAgent>()
            .AddSingleton<IChatQueue, ChatQueue>()
            .AddScoped<IIncomingMessageHandler, ChatInbox>()
            .AddScoped<SettingsProposalSender>()
            .AddScoped<ChatProcessor>();
}
