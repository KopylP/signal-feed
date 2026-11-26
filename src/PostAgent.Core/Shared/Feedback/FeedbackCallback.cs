using System.Globalization;

namespace PostAgent.Shared.Feedback;

// The text under a vote button, at most 64 bytes: "fb:<report id>:<entry number>:<u|d>". The entry number is the position in the
// snapshot of that digest, so a vote on an old message is told apart from the current digest by the report id.
public static class FeedbackCallback
{
    public const string Prefix = "fb:";

    public static string Encode(string reportId, int index, FeedbackVote vote) =>
        $"{Prefix}{reportId}:{index.ToString(CultureInfo.InvariantCulture)}:{(vote == FeedbackVote.Up ? 'u' : 'd')}";

    public static bool TryDecode(string? data, out string reportId, out int index, out FeedbackVote vote)
    {
        reportId = string.Empty;
        index = 0;
        vote = default;

        var parts = data?.Split(':');
        if (parts is not ["fb", { Length: > 0 } id, var number, var letter]
            || !int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out index)
            || letter is not ("u" or "d"))
        {
            return false;
        }

        reportId = id;
        vote = letter == "u" ? FeedbackVote.Up : FeedbackVote.Down;
        return true;
    }
}
