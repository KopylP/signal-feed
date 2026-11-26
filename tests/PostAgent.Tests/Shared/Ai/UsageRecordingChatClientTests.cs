using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Shared.Ai;
using PostAgent.Tests.Fakes;

namespace PostAgent.Tests.Shared.Ai;

public class UsageRecordingChatClientTests
{
    private readonly InMemoryUsageStore _store = new();

    private UsageRecordingChatClient Create(IChatClient inner) =>
        new(inner, _store, new FakeTimeProvider(TestData.Now), "chat", NullLogger<UsageRecordingChatClient>.Instance);

    [Fact]
    public async Task Records_the_tokens_of_a_response_for_the_day_and_purpose()
    {
        await Create(new UsageClient(100, 20)).GetResponseAsync([new ChatMessage(ChatRole.User, "q")]);
        await Create(new UsageClient(50, 5)).GetResponseAsync([new ChatMessage(ChatRole.User, "q")]);

        var row = Assert.Single(_store.Rows);
        Assert.Equal(("2026-11-24", "chat", 2, 150L, 25L), (row.Day, row.Purpose, row.Calls, row.InputTokens, row.OutputTokens));
    }

    [Fact]
    public async Task Records_the_tokens_of_a_streamed_response_once_at_the_end()
    {
        var client = Create(new UsageClient(100, 20));

        await foreach (var _ in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "q")]))
        {
        }

        var row = Assert.Single(_store.Rows);
        Assert.Equal((1, 100L, 20L), (row.Calls, row.InputTokens, row.OutputTokens));
    }

    [Fact]
    public async Task A_response_without_usage_records_nothing()
    {
        await Create(new UsageClient(null, null)).GetResponseAsync([new ChatMessage(ChatRole.User, "q")]);

        Assert.Empty(_store.Rows);
    }

    [Fact]
    public async Task A_storage_failure_does_not_touch_the_call()
    {
        _store.Throw = true;

        var response = await Create(new UsageClient(1, 1)).GetResponseAsync([new ChatMessage(ChatRole.User, "q")]);

        Assert.Equal("ok", response.Text);
    }

    private sealed class UsageClient(long? input, long? output) : IChatClient
    {
        private UsageDetails? Usage => input is null ? null : new UsageDetails { InputTokenCount = input, OutputTokenCount = output };

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")) { Usage = Usage });

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, "ok");
            if (Usage is { } usage)
            {
                yield return new ChatResponseUpdate { Contents = [new UsageContent(usage)] };
            }

            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
