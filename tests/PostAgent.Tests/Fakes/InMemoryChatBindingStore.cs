using PostAgent.Shared.Notifications;

namespace PostAgent.Tests.Fakes;

internal sealed class InMemoryChatBindingStore(long? bound = null) : IChatBindingStore
{
    public long? Bound { get; private set; } = bound;

    public Task<long?> GetAsync(CancellationToken ct) => Task.FromResult(Bound);

    public Task<bool> TryBindAsync(long chatId, CancellationToken ct)
    {
        if (Bound is not null)
        {
            return Task.FromResult(false);
        }

        Bound = chatId;
        return Task.FromResult(true);
    }
}
