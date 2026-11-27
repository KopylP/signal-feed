using Microsoft.Extensions.AI;
using PostAgent.Shared.Posts;

namespace PostAgent.Shared.Ai;

public static class EmbeddingGeneratorExtensions
{
    // One vector per text, in order, of PostVectors.Dimensions; a long text is cut to what the model reads.
    public static async Task<IReadOnlyList<float[]>> EmbedAsync(this IEmbeddingGenerator<string, Embedding<float>> generator, IEnumerable<string> texts, CancellationToken ct)
    {
        var input = texts.Select(t => t.Length <= PostVectors.MaxTextChars ? t : t[..PostVectors.MaxTextChars]).ToList();
        var embeddings = await generator.GenerateAsync(input, new EmbeddingGenerationOptions { Dimensions = PostVectors.Dimensions }, ct);
        return embeddings.Select(e => e.Vector.ToArray()).ToList();
    }
}
