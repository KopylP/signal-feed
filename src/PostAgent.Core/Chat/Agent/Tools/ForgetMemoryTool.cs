using System.ComponentModel;
using Microsoft.Extensions.AI;
using PostAgent.Chat.Memory;

namespace PostAgent.Chat.Agent.Tools;

// Not invasive (ADR-0022): it only removes a note of this chat's reader memory, and the reader sees a line about it under the answer.
public sealed class ForgetMemoryTool(ReaderMemory memory) : IChatTool
{
    public AIFunction Build(ChatTurn turn) => AIFunctionFactory.Create(
        ([Description("The file name of the note exactly as <reader_memory> lists it.")] string note, CancellationToken ct) => RunAsync(turn, note, ct),
        new AIFunctionFactoryOptions
        {
            Name = "forget_memory",
            Description = "Remove a whole note from the reader memory now, no confirmation needed: when the reader asks to forget it, when it is no longer true, or to make room. To drop only some facts, update the note with save_memory instead.",
        });

    private async Task<string> RunAsync(ChatTurn turn, string? note, CancellationToken ct)
    {
        var edit = await memory.ForgetAsync(turn.ChatId, note, ct);
        turn.RecordMemory(edit);
        return edit.Answer;
    }
}
