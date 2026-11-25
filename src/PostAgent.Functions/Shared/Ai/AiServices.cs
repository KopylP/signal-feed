using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using PostAgent.Shared.Settings;

namespace PostAgent.Shared.Ai;

public static class AiServices
{
    // Key of the chat client: the chat feature uses a stronger model than scoring and the digest (which use the unkeyed one).
    public const string ChatClientKey = "chat";

    // Модель чи провайдера міняють лише тут: решта коду знає тільки IChatClient.
    public static IServiceCollection AddAi(this IServiceCollection services)
    {
        services.AddChatClient(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
            return new OpenAI.Chat.ChatClient(settings.OpenAIModel, settings.OpenAIApiKey).AsIChatClient();
        });
        services.AddKeyedChatClient(ChatClientKey, sp =>
        {
            var settings = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
            return new OpenAI.Chat.ChatClient(settings.OpenAIChatModel, settings.OpenAIApiKey).AsIChatClient();
        });
        return services;
    }
}
