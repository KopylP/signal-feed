using System.Text.Json;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Agent;

// The answer to send, the agent session to keep for the next question, the invasive changes the agent prepared for the owner's tap
// (none for most answers), whether the reader asked to forget the conversation (the session is then dropped instead of kept),
// and the options to show as buttons under the answer (a tap sends the option back as the reader's next message).
public sealed record ChatReply(
    string Text,
    JsonElement Session,
    IReadOnlyList<SettingChange>? Proposed = null,
    bool ClearsContext = false,
    IReadOnlyList<string>? Choices = null);
