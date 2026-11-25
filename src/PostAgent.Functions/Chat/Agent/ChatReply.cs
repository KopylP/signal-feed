using System.Text.Json;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Agent;

// The answer to send, the agent session to keep for the next question and the settings changes the agent prepared (none for most answers).
public sealed record ChatReply(string Text, JsonElement Session, IReadOnlyList<SettingChange>? Proposed = null);
