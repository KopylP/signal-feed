using System.Text.Json;

namespace PostAgent.Shared.Feedback;

public static class FeedbackJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    // Never throws: a damaged file or a damaged item must not stop the votes, so it reads as "nothing" and the next vote replaces it.
    public static IReadOnlyList<FeedbackItem> Parse(string json)
    {
        try
        {
            return (JsonSerializer.Deserialize<List<FeedbackItem?>>(json, Options) ?? [])
                .OfType<FeedbackItem>()
                .Where(i => i.PostId is not null && i.Source is not null && i.Topic is not null && Enum.IsDefined(i.Vote))
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
