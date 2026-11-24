using System.Text.Json.Serialization;

namespace PostAgent.Digest.Infrastructure;

public sealed record HeadlineEntry(
    string Id,
    string Title,
    string Summary,
    [property: JsonPropertyName("why_it_matters")] string WhyItMatters);
