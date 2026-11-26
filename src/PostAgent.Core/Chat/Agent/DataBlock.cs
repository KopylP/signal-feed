namespace PostAgent.Chat.Agent;

// Text written by other people (channels) goes to the model inside a tag, with every "<" escaped so that it cannot close the tag.
public static class DataBlock
{
    public static string Wrap(string tag, string text) => $"<{tag}>{Escape(text)}</{tag}>";

    public static string Escape(string text) => text.Replace("<", "&lt;", StringComparison.Ordinal);
}
