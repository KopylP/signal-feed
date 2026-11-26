namespace PostAgent.Shared.Common;

public static class TextExtensions
{
    public static string Truncate(this string text, int max, string suffix = "")
    {
        if (text.Length <= max)
        {
            return text;
        }

        var cut = max > 0 && char.IsHighSurrogate(text[max - 1]) ? max - 1 : max;
        return text[..cut] + suffix;
    }
}
