using System.Text.Json;

namespace PostAgent.Shared.Stats;

public static class StatsJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    // Never throws: a damaged file reads as nothing, so one bad blob cannot stop /stats or the next aggregation.
    public static T? TryParse<T>(string json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
