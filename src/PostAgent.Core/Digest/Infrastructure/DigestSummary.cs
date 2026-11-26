using System.Text.Json.Serialization;

namespace PostAgent.Digest.Infrastructure;

// The strict response schema of the digest model. Entries refer to posts by id and carry only text:
// the channel, the date and the link are added from the stored post, so the model cannot invent them.
public sealed record DigestSummary(
    string Overview,
    List<HeadlineEntry> Headlines,
    [property: JsonPropertyName("for_marketplace")] List<NoteEntry> ForMarketplace,
    List<NotableEntry> Notable,
    List<PickEntry> Recommended,
    [property: JsonPropertyName("needs_verification")] List<string> NeedsVerification);
