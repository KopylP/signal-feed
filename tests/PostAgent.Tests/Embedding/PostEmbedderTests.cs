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
    private readonly FakeEmbeddingLock _lock = new();

    private static PostEntity New(string key, string text, double hoursAgo = 1)
    {
        var post = Post(key, score: null, hoursAgo: hoursAgo, status: PostStatus.New);
        post.Text = text;
        return post;
    }

    private PostEmbedder Embedder(IPostRepository repo) =>
        new(repo, _embeddings, _scoring, _lock, new FakeEffectiveOptions(Options(o => o.ScoringBatchSize = 3)), new FakeTimeProvider(Now), NullLogger<PostEmbedder>.Instance);

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
    public async Task Calls_the_model_outside_the_lock_and_checks_and_saves_inside_it()
    {
        var a = New("a", "text");
        var repo = new InMemoryPostRepository(a);
        int? callsWhenTaken = null, savedWhenReleased = null;
        _lock.OnAcquire = () => callsWhenTaken = _embeddings.Calls.Count;
        _lock.OnRelease = () => savedWhenReleased = repo.EmbeddingsSaved.Count;

        await Embedder(repo).EmbedAsync([a.ToRef()], CancellationToken.None);

        Assert.Equal(1, callsWhenTaken);
        Assert.Equal(1, savedWhenReleased);
        Assert.Equal((1, 1), (_lock.Acquired, _lock.Released));
        Assert.Equal([a.ToRef()], _scoring.Enqueued);
    }

    [Fact]
    public async Task Releases_the_lock_when_the_save_fails()
    {
        var a = New("a", "text");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Embedder(new FailingSaveRepository(a)).EmbedAsync([a.ToRef()], CancellationToken.None));

        Assert.Equal((1, 1), (_lock.Acquired, _lock.Released));
        Assert.Empty(_scoring.Enqueued);
    }

    [Fact]
    public async Task Queues_the_posts_without_vectors_when_the_embedding_call_fails()
    {
        _embeddings.Failure = new InvalidOperationException("OpenAI is down");
        var a = New("a", "text");
        var repo = new InMemoryPostRepository(a);

        await Embedder(repo).EmbedAsync([a.ToRef()], CancellationToken.None);

        Assert.Empty(repo.EmbeddingsSaved);
        Assert.Equal(0, _lock.Acquired);
        Assert.Equal([a.ToRef()], _scoring.Enqueued);
    }

    [Fact]
    public async Task A_retry_after_a_failed_queue_send_queues_again_without_a_second_embedding_call()
    {
        _embeddings.With("Google started the core update", 1, 0);
        var earlier = New("first", "Google rolls out the core update", hoursAgo: 3);
        earlier.Status = PostStatus.Scored;
        earlier.Embedding = PostVectors.ToBytes((float[])[1, 0, .. new float[510]]);
        var repeat = New("second", "Google started the core update");
        var other = New("third", "Bing adds AI answers");
        var repo = new InMemoryPostRepository(earlier, repeat, other) { CopyOnRead = true };
        _scoring.Failure = new InvalidOperationException("queue down");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Embedder(repo).EmbedAsync([repeat.ToRef(), other.ToRef()], CancellationToken.None));
        _scoring.Failure = null;
        await Embedder(repo).EmbedAsync([repeat.ToRef(), other.ToRef()], CancellationToken.None);

        Assert.Single(_embeddings.Calls);
        Assert.Equal([other.ToRef()], _scoring.Enqueued);
        Assert.Equal("first", repeat.DuplicateOf);
    }

    [Fact]
    public async Task A_post_handled_after_its_repost_is_the_one_dropped_even_if_published_first()
    {
        _embeddings.With("Repost: Google core update", 1, 0).With("Google core update", 0.99f, 0.05f);
        var original = New("a", "Google core update", hoursAgo: 2);
        var repost = New("b", "Repost: Google core update", hoursAgo: 1);
        var repo = new InMemoryPostRepository(original, repost) { CopyOnRead = true };

        await Embedder(repo).EmbedAsync([repost.ToRef()], CancellationToken.None);
        await Embedder(repo).EmbedAsync([original.ToRef()], CancellationToken.None);

        Assert.Equal([repost.ToRef()], _scoring.Enqueued);
        Assert.Equal("b", original.DuplicateOf);
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

    private sealed class FailingSaveRepository(params PostEntity[] posts) : IPostRepository
    {
        private readonly InMemoryPostRepository _inner = new(posts);

        public Task SaveEmbeddingsAsync(IEnumerable<PostEntity> posts, CancellationToken ct) => throw new InvalidOperationException("storage is down");

        public Task AddIfMissingAsync(PostEntity post, CancellationToken ct) => _inner.AddIfMissingAsync(post, ct);

        public Task<IReadOnlyList<PostEntity>> GetAsync(IEnumerable<PostRef> refs, CancellationToken ct) => _inner.GetAsync(refs, ct);

        public Task<IReadOnlyList<PostEntity>> GetSinceAsync(DateTimeOffset since, DateTimeOffset now, CancellationToken ct) => _inner.GetSinceAsync(since, now, ct);

        public Task SaveScoresAsync(IEnumerable<PostEntity> posts, CancellationToken ct) => _inner.SaveScoresAsync(posts, ct);

        public Task MarkSentAsync(IEnumerable<PostEntity> posts, DateTimeOffset? alertedAt, CancellationToken ct) => _inner.MarkSentAsync(posts, alertedAt, ct);

        public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct) => _inner.DeleteOlderThanAsync(cutoff, ct);
    }
}
