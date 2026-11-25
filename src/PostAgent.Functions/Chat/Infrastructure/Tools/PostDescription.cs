using System.Globalization;
using System.Text;
using PostAgent.Shared.Chat;
using PostAgent.Shared.Posts;

namespace PostAgent.Chat.Infrastructure.Tools;

public static class PostDescription
{
    // The topic and the source name come from channel text too, so the details travel inside the block with the post.
    public static string Of(PostEntity post)
    {
        var line = new StringBuilder();
        line.Append("id: ").Append(ChatPostId.Format(post.ToRef()));
        line.Append(" | @").Append(post.Source);
        line.Append(" | ").Append(post.PublishedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append(" UTC");
        if (post.Topic is not null)
        {
            line.Append(" | ").Append(post.Topic);
        }

        if (post.Score is not null)
        {
            line.Append(" | score ").Append(post.Score.Value.ToString(CultureInfo.InvariantCulture));
        }

        return line.Append(" | ").Append(post.Url).ToString();
    }
}
