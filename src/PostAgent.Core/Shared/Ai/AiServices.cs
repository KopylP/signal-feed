using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using PostAgent.Shared.Settings;
using PostAgent.Shared.Stats;

namespace PostAgent.Shared.Ai;

public static class AiServices
{
    // Key of the chat client: the chat feature uses a stronger model than scoring and the digest (which use the unkeyed one).
    public const string ChatClientKey = "chat";

    // The provider is changed only here; the model is picked on every request (ModelSelectingChatClient), so the bot can change it.
    public static IServiceCollection AddAi(this IServiceCollection services)
    {
        services.AddChatClient(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
            return Recording(sp, "scoring", new ModelSelectingChatClient(
                new OpenAI.Chat.ChatClient(settings.OpenAIModel, settings.OpenAIApiKey).AsIChatClient(),
                sp.GetRequiredService<IEffectiveOptions>(),
                o => o.OpenAIModel));
        });
        services.AddKeyedChatClient(ChatClientKey, sp => Recording(sp, "chat", new ModelSelectingChatClient(
            CreateChatClient(sp.GetRequiredService<IOptions<AgentOptions>>().Value),
            sp.GetRequiredService<IEffectiveOptions>(),
            o => o.OpenAIChatModel)));
        return services;
    }

    private static IChatClient Recording(IServiceProvider sp, string purpose, IChatClient inner) => new UsageRecordingChatClient(
        inner, sp.GetRequiredService<IUsageStore>(), sp.GetRequiredService<TimeProvider>(), purpose, sp.GetRequiredService<ILogger<UsageRecordingChatClient>>());

    // The Responses API, not Chat Completions: OpenAI rejects function tools together with reasoning on /v1/chat/completions (HTTP 400).
    // The options parameter lets a test put its own transport under the real client.
    public static IChatClient CreateChatClient(AgentOptions settings, OpenAIClientOptions? options = null) =>
        new OpenAIClient(new ApiKeyCredential(settings.OpenAIApiKey), options).GetResponsesClient().AsIChatClient(settings.OpenAIChatModel);
}
