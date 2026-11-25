using PostAgent.Shared.Posts;

namespace PostAgent.Shared.Chat;

// The id the chat agent uses for a post: "<PartitionKey>/<RowKey>", enough for a point read from the Posts table.
public static class ChatPostId
{
    public static string Format(PostRef post) => $"{post.PartitionKey}/{post.RowKey}";

    public static bool TryParse(string id, out PostRef post)
    {
        var parts = id.Split('/');
        if (parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0)
        {
            post = new PostRef(parts[0], parts[1]);
            return true;
        }

        post = new PostRef(string.Empty, string.Empty);
        return false;
    }
}
