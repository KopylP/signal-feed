using System.ComponentModel;
using Microsoft.Extensions.AI;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Agent.Tools;

public sealed class ProposeAddWatchTool : IChatTool
{
    public AIFunction Build(ProposalPlan plan) => AIFunctionFactory.Create(
        ([Description("The topic to follow, a short phrase like: google core update. Every word of it must be in a post.")] string phrase, CancellationToken ct) => RunAsync(plan, phrase, ct),
        new AIFunctionFactoryOptions
        {
            Name = "propose_add_watch",
            Description = "Plan to follow a topic: from then on a new scored post from any source that has every word of the phrase is sent as its own alert, whatever its score (not during quiet hours). Use it when the reader asks to watch, follow or track a topic.",
        });

    private static Task<string> RunAsync(ProposalPlan plan, string phrase, CancellationToken ct) =>
        WatchPhrases.Normalize(phrase) is { } normalized
            ? plan.AddAsync(new SettingChange(SettingChangeKind.AddWatch, normalized), ct)
            : Task.FromResult($"This is not a usable topic: it must be a short text of at most {WatchPhrases.MaxChars} characters.");
}
