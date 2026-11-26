using Microsoft.Extensions.AI;
using PostAgent.Shared.Chat;

namespace PostAgent.Chat.Agent.Tools;

// What the bot can do, from the code's own list (BotCommands), so the answer cannot go stale when a command is added.
public sealed class GetCommandsTool : IChatTool
{
    public AIFunction Build(ChatTurn turn) => AIFunctionFactory.Create(
        () => string.Join("\n", BotCommands.All.Select(c => $"{c.Usage} - {c.Purpose}")) + "\n" + BotCommands.ByMessage,
        new AIFunctionFactoryOptions
        {
            Name = "get_commands",
            Description = "The commands of the bot and what each is for. Call it when the reader asks what the bot can do, which commands exist or how to get something.",
        });
}
