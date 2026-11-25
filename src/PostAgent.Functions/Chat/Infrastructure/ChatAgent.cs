using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI.Responses;
using PostAgent.Shared.Ai;
using PostAgent.Shared.Chat;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Infrastructure;

// Microsoft Agent Framework agent over the chat model. One conversation is the digest snapshot (the first message)
// plus the questions and answers; the session is serialized after every turn and stored with the snapshot.
public sealed class ChatAgent(
    [FromKeyedServices(AiServices.ChatClientKey)] IChatClient chat,
    ChatTools tools,
    IEffectiveOptions options,
    ILogger<ChatAgent> log) : IChatAgent
{
    private readonly string _instructions = Prompts.Load("chat.md");

    public async Task<ChatReply> AskAsync(ChatState state, string userText, CancellationToken ct)
    {
        var settings = await options.GetAsync(ct);
        var agent = CreateAgent(settings);

        var (session, hasHistory) = await OpenSessionAsync(agent, state.Session, ct);
        var messages = new List<ChatMessage>();
        if (!hasHistory)
        {
            messages.Add(new ChatMessage(ChatRole.User, ChatContext.Build(state.Report)));
        }

        messages.Add(new ChatMessage(ChatRole.User, userText));

        var response = await agent.RunAsync(messages, session, cancellationToken: ct);
        if (string.IsNullOrWhiteSpace(response.Text))
        {
            // After ChatMaxToolCalls the function-calling layer asks once more without tools, so a model normally still answers in text;
            // this is a truly empty answer.
            throw new InvalidOperationException("The model returned no text.");
        }

        var serialized = await agent.SerializeSessionAsync(session, cancellationToken: ct);
        return new ChatReply(response.Text.Trim(), serialized.Clone());
    }

    private AIAgent CreateAgent(AgentOptions settings)
    {
        var client = new ChatClientBuilder(chat)
            .UseFunctionInvocation(configure: c => c.MaximumIterationsPerRequest = settings.ChatMaxToolCalls)
            .Build();

        return client.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "chat",
            ChatOptions = new ChatOptions
            {
                Instructions = _instructions,
                MaxOutputTokens = settings.ChatMaxOutputTokens,
                // The conversation is ours (the blob), not OpenAI's: without this the Responses API returns a response id that the agent framework
                // takes for server-side history and refuses to combine with our own. With nothing stored on their side, the hidden reasoning
                // must come back encrypted so that it can be sent along with the next request after a tool call.
                RawRepresentationFactory = _ => new CreateResponseOptions
                {
                    StoredOutputEnabled = false,
                    IncludedProperties = { IncludedResponseProperty.ReasoningEncryptedContent },
                },
                Reasoning = new ReasoningOptions { Effort = Enum.Parse<ReasoningEffort>(settings.ChatReasoningEffort, ignoreCase: true) },
                Tools =
                [
                    AIFunctionFactory.Create(tools.GetPost, "get_post"),
                    AIFunctionFactory.Create(tools.SearchPosts, "search_posts"),
                ],
            },
            ChatHistoryProvider = new InMemoryChatHistoryProvider(
                new InMemoryChatHistoryProviderOptions { ChatReducer = new ChatHistoryReducer(settings.ChatMaxMessages) }),
        });
    }

    // A session that cannot be read, or that reads as an empty history, is replaced by a fresh one:
    // the snapshot is then sent again, so the conversation continues about the same digest.
    private async Task<(AgentSession Session, bool HasHistory)> OpenSessionAsync(AIAgent agent, JsonElement? stored, CancellationToken ct)
    {
        if (stored is { } element)
        {
            try
            {
                var session = await agent.DeserializeSessionAsync(element, cancellationToken: ct);
                var history = agent.GetService<InMemoryChatHistoryProvider>()?.GetMessages(session);
                if (history is { Count: > 0 })
                {
                    return (session, true);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "The stored chat session could not be read, starting a fresh one");
            }
        }

        return (await agent.CreateSessionAsync(ct), false);
    }
}
