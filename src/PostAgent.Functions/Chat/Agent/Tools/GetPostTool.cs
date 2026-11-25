using System.ComponentModel;
using Microsoft.Extensions.AI;
using PostAgent.Shared.Chat;
using PostAgent.Shared.Posts;

namespace PostAgent.Chat.Agent.Tools;

// Post text is data written by others: it is always inside a <post> block.
public sealed class GetPostTool(IPostRepository posts) : IChatTool
{
    private const string NotFound = "Post not found. It may have been deleted; rely on the digest text.";

    public AIFunction Build(ProposalPlan plan) => AIFunctionFactory.Create(
        ([Description("The post id from the digest or from a search result.")] string id, CancellationToken ct) => RunAsync(id, ct),
        new AIFunctionFactoryOptions
        {
            Name = "get_post",
            Description = "Get the full text and details of one stored post by its id (the id shown in the digest, like 2026-11-25/channel_123).",
        });

    private async Task<string> RunAsync(string id, CancellationToken ct)
    {
        if (!ChatPostId.TryParse(id, out var reference))
        {
            return NotFound;
        }

        var post = (await posts.GetAsync([reference], ct)).FirstOrDefault();
        return post is null ? NotFound : DataBlock.Wrap("post", PostDescription.Of(post) + "\n" + post.Text);
    }
}
