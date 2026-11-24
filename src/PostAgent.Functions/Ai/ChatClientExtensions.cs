using Microsoft.Extensions.AI;

namespace PostAgent.Ai;

public static class ChatClientExtensions
{
    public static async Task<T> AskAsync<T>(this IChatClient chat, string systemPrompt, string userMessage, CancellationToken ct)
    {
        var options = new ChatOptions { AdditionalProperties = new AdditionalPropertiesDictionary { ["strict"] = true } };

        var response = await chat.GetResponseAsync<T>(
            [new ChatMessage(ChatRole.System, systemPrompt), new ChatMessage(ChatRole.User, userMessage)],
            options,
            cancellationToken: ct);

        return response.Result;
    }
}
