using System.Text.RegularExpressions;
using PostAgent.Shared.Posts;

namespace PostAgent.Alerts;

// A post matches a followed topic when every word of the phrase is in its text or topic, in any order and any case.
// A short word (up to three letters, like "ai") must stand alone, so "ai" does not match "said"; a longer one may be part of a word ("update" in "updated").
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
        if (word.Length > WholeWordUpTo)
        {
            return haystack.Contains(word, StringComparison.OrdinalIgnoreCase);
        }

        try
        {
            return Regex.IsMatch(
                haystack,
                $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(word)}(?![\p{{L}}\p{{N}}])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                MatchTimeout);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
