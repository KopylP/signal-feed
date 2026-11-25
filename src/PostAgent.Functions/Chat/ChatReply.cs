using System.Text.Json;

namespace PostAgent.Chat;

// The answer to send and the agent session to keep for the next question.
public sealed record ChatReply(string Text, JsonElement Session);
