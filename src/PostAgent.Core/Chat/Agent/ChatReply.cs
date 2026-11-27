using System.Text.Json;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Agent;

// The answer to send, the agent session to keep for the next question, the invasive changes the agent prepared for the owner's tap
// (none for most answers), whether the reader asked to forget the conversation (the session is then dropped instead of kept),
// the options to show as buttons under the answer (a tap sends the option back as the reader's next message),
// and the lines that tell the reader what the agent changed in their memory (ADR-0022), built by code and sent under the answer.
public sealed record ChatReply(
    string Text,
    JsonElement Session,
    IReadOnlyList<SettingChange>? Proposed = null,
    bool ClearsContext = false,
    IReadOnlyList<string>? Choices = null,
    IReadOnlyList<string>? MemoryNotices = null)
{
    // The text the reader gets: the model's answer, then the memory lines. The session keeps only the model's answer.
    public string Message => MemoryNotices is { Count: > 0 } notices ? Text + "\n\n" + string.Join('\n', notices) : Text;
}
