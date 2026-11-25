using Microsoft.Extensions.AI;
using PostAgent.Chat.Infrastructure.Tools;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Infrastructure;

// All the tools of the chat agent for one question. The ones that propose a change share that question's plan, so each question gets its own.
public sealed class ChatToolFactory(IEnumerable<IChatTool> tools, SettingsEditor editor)
{
    public IReadOnlyList<AIFunction> Create(List<SettingChange> draft, long chatId)
    {
        var plan = new ProposalPlan(editor, draft, chatId);
        return tools.Select(t => t.Build(plan)).ToList();
    }
}
