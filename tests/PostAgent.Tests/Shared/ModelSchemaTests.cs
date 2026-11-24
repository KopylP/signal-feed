using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using PostAgent.Digest.Infrastructure;
using PostAgent.Scoring.Infrastructure;

namespace PostAgent.Tests.Shared;

// OpenAI strict structured output accepts a "$ref" only when it points into "$defs". A type used in two places of the schema makes the
// generator point the second use at the first one ("#/properties/..."), and OpenAI answers 400. The fake chat client cannot see it.
public partial class ModelSchemaTests
{
    [GeneratedRegex("\"\\$ref\"\\s*:\\s*\"([^\"]+)\"")]
    private static partial Regex RefPattern();

    [Theory]
    [InlineData(typeof(DigestSummary))]
    [InlineData(typeof(ScoredPosts))]
    public void The_response_schema_references_only_definitions(Type type)
    {
        var schema = AIJsonUtilities.CreateJsonSchema(type, serializerOptions: AIJsonUtilities.DefaultOptions).GetRawText();

        var references = RefPattern().Matches(schema).Select(m => m.Groups[1].Value).ToList();

        Assert.All(references, reference => Assert.StartsWith("#/$defs/", reference));
    }
}
