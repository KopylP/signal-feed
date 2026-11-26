using Microsoft.Extensions.AI;

namespace PostAgent.Chat.Agent.Tools;

// Not invasive (ADR-0021): the digest stays and the reader can type /clear as well. The conversation is dropped by ChatProcessor
// once this answer is saved, because the turn that is running still writes it.
public sealed class ClearContextTool : IChatTool
{
    public AIFunction Build(ChatTurn turn) => AIFunctionFactory.Create(
        () =>
        {
            turn.ClearContextAfterAnswer();
            return "Done: the earlier questions and answers are forgotten after this answer; the digest stays. Tell the reader.";
        },
        new AIFunctionFactoryOptions
        {
            Name = "clear_context",
            Description = "Clear the conversation context, no confirmation needed: the earlier questions and answers are forgotten after this answer, the digest stays. Only when the reader asks to forget, reset or start over.",
        });
}
