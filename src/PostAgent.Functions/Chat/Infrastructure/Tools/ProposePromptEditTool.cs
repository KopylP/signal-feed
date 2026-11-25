using System.ComponentModel;
using Microsoft.Extensions.AI;
using PostAgent.Shared.Ai;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Infrastructure.Tools;

public sealed class ProposePromptEditTool(PromptLibrary prompts) : IChatTool
{
    public AIFunction Build(ProposalPlan plan) => AIFunctionFactory.Create(
        (
            [Description("The section id from get_prompt, like scoring-interests.")] string section,
            [Description("The fragment to replace, copied exactly.")] string old_text,
            [Description("The new text.")] string new_text,
            CancellationToken ct) => RunAsync(plan, section, old_text, new_text, ct),
        new AIFunctionFactoryOptions
        {
            Name = "propose_prompt_edit",
            Description = "Plan to replace one fragment of an editable prompt section with another. old_text must be copied exactly from the section (get_prompt) and appear once; empty old_text adds new_text at the end; empty new_text removes the fragment. At most 500 characters each.",
        });

    private Task<string> RunAsync(ProposalPlan plan, string section, string oldText, string newText, CancellationToken ct) =>
        prompts.FindSection(section) is { } found
            ? plan.AddAsync(new SettingChange(SettingChangeKind.EditPrompt, found.Id, newText, oldText), ct)
            : Task.FromResult("Unknown section. Call get_prompt and use an id of an editable section.");
}
