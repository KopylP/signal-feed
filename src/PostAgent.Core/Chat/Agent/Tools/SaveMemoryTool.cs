using System.ComponentModel;
using Microsoft.Extensions.AI;
using PostAgent.Chat.Memory;

namespace PostAgent.Chat.Agent.Tools;

// Not invasive (ADR-0022): a note only shapes the answers to this chat's reader, is shown to them by a line under the answer and is undone by forget_memory.
public sealed class SaveMemoryTool(ReaderMemory memory) : IChatTool
{
    public AIFunction Build(ChatTurn turn) => AIFunctionFactory.Create(
        (
            [Description("Empty for a new note; to update a note, its file name exactly as <reader_memory> lists it. The whole note is replaced by what you give now.")] string note,
            [Description("A short title of the topic, like: Робота і цілі.")] string title,
            [Description("1 to 5 keywords of what the note is about; they become the file name.")] string[] keywords,
            [Description("The facts, one short line each; a line starting with '## ' is a sub-heading. Aim for at most 20 lines.")] string[] facts,
            CancellationToken ct) => RunAsync(turn, note, title, keywords, facts, ct),
        new AIFunctionFactoryOptions
        {
            Name = "save_memory",
            Description = $"Save or update a note in the reader memory now, no confirmation needed. Only for lasting facts about the reader that they said themselves (work, goals, interests, preferences for answers). One topic per note; when a topic already has a note, update that note instead of adding one. The memory holds at most {MemoryNoteFormat.MaxNotes} notes of at most {MemoryNoteFormat.MaxFactLines} lines.",
        });

    private async Task<string> RunAsync(ChatTurn turn, string? note, string? title, string[]? keywords, string[]? facts, CancellationToken ct)
    {
        var edit = await memory.RememberAsync(turn.ChatId, note, title, keywords, facts, ct);
        turn.RecordMemory(edit);
        return edit.Answer;
    }
}
