using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Embedding;
using PostAgent.Shared.Posts;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Embedding;

public class PostEmbedderTests
{
    private readonly FakeEmbeddingGenerator _embeddings = new();
    private readonly FakePostQueue _scoring = new();

    private static PostEntity New(string key, string text, double hoursAgo = 1)
    {
        var post = Post(key, score: null, hoursAgo: hoursAgo, status: PostStatus.New);
        post.Text = text;
        return post;
    }

    private PostEmbedder Embedder(InMemoryPostRepository repo) =>
        new(repo, _embeddings, _scoring, new FakeEffectiveOptions(Options(o => o.ScoringBatchSize = 3)), new FakeTimeProvider(Now), NullLogger<PostEmbedder>.Instance);

    [Fact]
    public async Task Saves_the_vectors_of_new_posts_and_queues_them_for_scoring()
    {
        var a = New("a", "Google core update");
        var b = New("b", "Bing adds AI answers");
        var repo = new InMemoryPostRepository(a, b);

        await Embedder(repo).EmbedAsync([a.ToRef(), b.ToRef()], CancellationToken.None);

        Assert.Equal(["a", "b"], repo.EmbeddingsSaved.Select(p => p.RowKey));
        Assert.All(repo.EmbeddingsSaved, p => Assert.Equal(512 * sizeof(float), p.Embedding?.Length));
        Assert.Equal([a.ToRef(), b.ToRef()], _scoring.Enqueued);
        Assert.Equal(3, _scoring.BatchSize);
    }

    [Fact]
    public async Task Does_not_queue_a_near_duplicate_of_a_post_from_another_source()
    {
        _embeddings.With("Google rolls out the November core update", 1, 0).With("Google started the core update of November", 0.98f, 0.1f);
        var earlier = New("first", "Google rolls out the November core update", hoursAgo: 3);
        earlier.Status = PostStatus.Scored;
        earlier.Score = 8;
        earlier.Embedding = PostVectors.ToBytes((float[])[1, 0, .. new float[510]]);
        var repeat = New("second", "Google started the core update of November");
        var repo = new InMemoryPostRepository(earlier, repeat);

        await Embedder(repo).EmbedAsync([repeat.ToRef()], CancellationToken.None);

        Assert.Empty(_scoring.Enqueued);
        var saved = Assert.Single(repo.EmbeddingsSaved);
        Assert.Equal("first", saved.DuplicateOf);
        Assert.Equal(PostStatus.Scored, saved.Status);
    }

    [Fact]
    public async Task Queues_the_posts_without_vectors_when_the_embedding_call_fails()
    {
        _embeddings.Failure = new InvalidOperationException("OpenAI is down");
        var a = New("a", "text");
        var repo = new InMemoryPostRepository(a);

        await Embedder(repo).EmbedAsync([a.ToRef()], CancellationToken.None);

        Assert.Empty(repo.EmbeddingsSaved);
        Assert.Equal([a.ToRef()], _scoring.Enqueued);
    }

    [Fact]
    public async Task Skips_posts_that_are_already_scored_so_a_repeated_message_is_harmless()
    {
        var scored = Post("a", score: 8);
        var repo = new InMemoryPostRepository(scored);

        await Embedder(repo).EmbedAsync([scored.ToRef()], CancellationToken.None);

        Assert.Empty(_embeddings.Calls);
        Assert.Empty(_scoring.Enqueued);
    }

    [Fact]
    public async Task The_function_reads_the_references_of_the_collector_message()
    {
        var a = New("a", "text");
        var repo = new InMemoryPostRepository(a);

        await new EmbeddingFunction(Embedder(repo)).Run(JsonSerializer.Serialize(new[] { a.ToRef() }), CancellationToken.None);

        Assert.Equal([a.ToRef()], _scoring.Enqueued);
    }
}
