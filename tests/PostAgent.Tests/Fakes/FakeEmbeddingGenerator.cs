using Microsoft.Extensions.AI;

namespace PostAgent.Tests.Fakes;

// A text gets the vector set for it (exact text), otherwise one of its own that is orthogonal to every other text.
internal sealed class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly Dictionary<string, float[]> _vectors = [];

    public Exception? Failure { get; set; }
    public List<IReadOnlyList<string>> Calls { get; } = [];

    public FakeEmbeddingGenerator With(string text, params float[] vector)
    {
        _vectors[text] = vector;
        return this;
    }

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var texts = values.ToList();
        Calls.Add(texts);
        if (Failure is not null)
        {
            throw Failure;
        }

        var size = options?.Dimensions ?? 4;
        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(texts.Select(t => new Embedding<float>(Vector(t, size)))));
    }

    private float[] Vector(string text, int size)
    {
        if (_vectors.TryGetValue(text, out var set))
        {
            return [.. set, .. new float[Math.Max(0, size - set.Length)]];
        }

        var own = new float[size];
        own[(int)((uint)StringComparer.Ordinal.GetHashCode(text) % (uint)size)] = 1;
        return own;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
