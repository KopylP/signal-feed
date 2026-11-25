using PostAgent.Shared.Notifications;

namespace PostAgent.Tests.Fakes;

internal sealed class InMemoryChatBindingStore(params long[] bound) : IChatBindingStore
{
    public List<long> Bound { get; } = [.. bound];

    public Task<IReadOnlyList<long>> GetAllAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<long>>(Bound.ToList());

    public Task<BindResult> TryBindAsync(long chatId, int max, CancellationToken ct)
    {
        if (Bound.Contains(chatId))
        {
            return Task.FromResult(BindResult.AlreadyBound);
        }

        if (Bound.Count >= max)
        {
            return Task.FromResult(BindResult.Full);
        }

        Bound.Add(chatId);
        return Task.FromResult(BindResult.Added);
    }
}
