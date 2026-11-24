using System.Text;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Posts;

namespace PostAgent.Alerts;

public static class AlertMessage
{
    public static string Format(IReadOnlyList<PostEntity> posts)
    {
        var message = new StringBuilder("🚨 <b>Щось відбувається</b>");
        foreach (var post in posts)
        {
            message.Append("\n\n<b>").Append(TelegramHtml.Escape(TelegramHtml.Topic(post))).Append("</b>");
            TelegramHtml.AppendPostDetails(message, post, post.Reason);
        }
        return message.ToString();
    }
}
