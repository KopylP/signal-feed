using System.Text;
using PostAgent.Shared.Notifications;

namespace PostAgent.Alerts;

// The alert for followed topics. Ukrainian: it goes to the end user. The topic is the owner's text, the post data comes from channels: both escaped.
public static class WatchAlertMessage
{
    public static string Format(IReadOnlyList<WatchHit> hits)
    {
        var message = new StringBuilder("👀 <b>Стеження за темою</b>");
        foreach (var hit in hits)
        {
            message.Append("\n\n<b>").Append(TelegramHtml.Escape(TelegramHtml.Topic(hit.Post))).Append("</b>");
            message.Append("\nТема: «").Append(TelegramHtml.Escape(hit.Phrase)).Append('»');
            TelegramHtml.AppendPostDetails(message, hit.Post, hit.Post.Reason);
        }

        return message.ToString();
    }
}
