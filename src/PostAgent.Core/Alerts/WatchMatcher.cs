using System.Text.RegularExpressions;
using PostAgent.Shared.Posts;

namespace PostAgent.Alerts;

// The word match of a followed topic, the fallback of the match by meaning (ADR-0024): every word of the phrase is in the text or topic, in any order and any case.
// A word must start a word of the text ("update" matches "updated", "core" does not match "score"); a short one (up to three letters, like "ai")
// must stand alone, so "ai" does not match "said" or "aim".
public static class WatchMatcher
{
    private const int WholeWordUpTo = 3;
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    public static bool Matches(PostEntity post, string phrase)
    {
        var words = phrase.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return false;
        }

        var haystack = post.Text + "\n" + post.Topic;
        return words.All(word => ContainsWord(haystack, word));
    }

    private static bool ContainsWord(string haystack, string word)
    {
        var end = word.Length <= WholeWordUpTo ? @"(?![\p{L}\p{N}])" : string.Empty;
        try
        {
            return Regex.IsMatch(
                haystack,
                $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(word)}{end}",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                MatchTimeout);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
