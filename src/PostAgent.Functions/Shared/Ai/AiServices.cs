using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
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
        services.AddKeyedChatClient(ChatClientKey, sp => CreateChatClient(sp.GetRequiredService<IOptions<AgentOptions>>().Value));
        return services;
    }

    // The Responses API, not Chat Completions: OpenAI rejects function tools together with reasoning on /v1/chat/completions (HTTP 400).
    // The options parameter lets a test put its own transport under the real client.
    public static IChatClient CreateChatClient(AgentOptions settings, OpenAIClientOptions? options = null) =>
        new OpenAIClient(new ApiKeyCredential(settings.OpenAIApiKey), options).GetResponsesClient().AsIChatClient(settings.OpenAIChatModel);
}
