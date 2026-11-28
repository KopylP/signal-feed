using System.ComponentModel;
using Microsoft.Extensions.AI;
using PostAgent.Shared.Ai;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Agent.Tools;

public sealed class ProposePromptRewriteTool(PromptLibrary prompts) : IChatTool
{
    public AIFunction Build(ChatTurn turn) => AIFunctionFactory.Create(
        (
            [Description("The section id from get_prompt, like scoring-interests.")] string section,
            [Description("The complete new text of the section, without its heading or markers.")] string new_text,
            CancellationToken ct) => RunAsync(turn.Plan, section, new_text, ct),
        new AIFunctionFactoryOptions
        {
            Name = "propose_prompt_rewrite",
            Description = $"Plan to replace the whole text of one editable prompt section, for a rewrite or a big cut that small edits cannot do. new_text is the complete section (up to {TextEdit.MaxText} characters, no headings or markers). Read the section with get_prompt first and keep what the reader wants to keep. The reader is shown the lines that change and applies it with a tap.",
        });

    private Task<string> RunAsync(ProposalPlan plan, string section, string newText, CancellationToken ct) =>
        prompts.FindSection(section) is { } found
            ? plan.AddAsync(new SettingChange(SettingChangeKind.RewritePrompt, found.Id, newText), ct)
            : Task.FromResult("Unknown section. Call get_prompt and use an id of an editable section.");
}
