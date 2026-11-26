using Microsoft.Extensions.AI;

namespace PostAgent.Chat.Agent.Tools;

// One tool of the chat agent, one class. Every class that implements it is found in the assembly and registered (ChatServices),
// so a new tool is a new file. The turn belongs to one question: a tool that proposes an invasive change adds it to the turn's plan,
// where nothing is stored; a non-invasive action (ADR-0021) is done by its tool at once.
public interface IChatTool
{
    AIFunction Build(ChatTurn turn);
}
