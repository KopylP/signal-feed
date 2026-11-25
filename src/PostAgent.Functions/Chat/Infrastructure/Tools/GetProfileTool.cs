using Microsoft.Extensions.AI;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Infrastructure.Tools;

public sealed class GetProfileTool(IProfileStore profile) : IChatTool
{
    public AIFunction Build(ProposalPlan plan) => AIFunctionFactory.Create(
        (CancellationToken ct) => RunAsync(plan.ChatId, ct),
        new AIFunctionFactoryOptions { Name = "get_profile", Description = "Show what the reader wrote about themselves (their profile). Empty when nothing is saved." });

    // The profile is text the owner typed or the model once proposed, so it is data here, like in the instructions.
    private async Task<string> RunAsync(long chatId, CancellationToken ct)
    {
        var text = (await profile.LoadAsync(chatId, ct))?.Text;
        return string.IsNullOrWhiteSpace(text) ? "(the profile is empty)" : DataBlock.Wrap("about_me", text.Trim());
    }
}
