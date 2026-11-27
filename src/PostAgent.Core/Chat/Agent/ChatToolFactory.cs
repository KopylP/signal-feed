using Microsoft.Extensions.AI;
using PostAgent.Chat.Agent.Tools;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Agent;

// All the tools of the chat agent for one question. They share that question's turn (and its plan), so each question gets its own.
public sealed class ChatToolFactory(IEnumerable<IChatTool> tools, SettingsProposals proposals, SettingsEditor editor)
{
    public ChatTurn NewTurn(long chatId) => new(new ProposalPlan(proposals, editor, [], chatId));

    public IReadOnlyList<AIFunction> Create(ChatTurn turn) => tools.Select(t => t.Build(turn)).ToList();
}
