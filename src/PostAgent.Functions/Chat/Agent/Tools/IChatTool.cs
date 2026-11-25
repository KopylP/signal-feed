using Microsoft.Extensions.AI;

namespace PostAgent.Chat.Agent.Tools;

// One tool of the chat agent, one class. Every class that implements it is found in the assembly and registered (ChatServices),
// so a new tool is a new file. The plan belongs to one question: a tool that proposes a change adds it there, and nothing is stored.
public interface IChatTool
{
    AIFunction Build(ProposalPlan plan);
}
