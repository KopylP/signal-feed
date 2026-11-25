using PostAgent.Chat.Infrastructure;
using PostAgent.Shared.Chat;

namespace PostAgent.Chat;

public static class ChatServices
{
    public static IServiceCollection AddChat(this IServiceCollection services) =>
        services.AddSingleton<IChatStateStore, BlobChatStateStore>();
}
