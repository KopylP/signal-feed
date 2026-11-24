using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using PostAgent.Shared.Settings;

namespace PostAgent.Shared.Ai;

public static class AiServices
{
    // Модель чи провайдера міняють лише тут: решта коду знає тільки IChatClient.
    public static IServiceCollection AddAi(this IServiceCollection services)
    {
        services.AddChatClient(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
            return new OpenAI.Chat.ChatClient(settings.OpenAIModel, settings.OpenAIApiKey).AsIChatClient();
        });
        return services;
    }
}
