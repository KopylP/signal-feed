using Microsoft.Extensions.AI;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Infrastructure.Tools;

public sealed class GetProfileTool(IProfileStore profile) : IChatTool
{
    public AIFunction Build(ProposalPlan plan) => AIFunctionFactory.Create(
        (CancellationToken ct) => RunAsync(ct),
        new AIFunctionFactoryOptions { Name = "get_profile", Description = "Show what the reader wrote about themselves (their profile). Empty when nothing is saved." });

    private async Task<string> RunAsync(CancellationToken ct)
    {
        var text = (await profile.LoadAsync(ct))?.Text;
        return string.IsNullOrWhiteSpace(text) ? "(the profile is empty)" : text;
    }
}
