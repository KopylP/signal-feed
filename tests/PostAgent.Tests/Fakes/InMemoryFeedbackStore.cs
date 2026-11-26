using PostAgent.Shared.Feedback;

namespace PostAgent.Tests.Fakes;

public sealed class InMemoryFeedbackStore : IFeedbackStore
{
    private IReadOnlyList<FeedbackItem> _items = [];

    public bool ThrowOnLoad { get; set; }

    public bool ThrowOnRecord { get; set; }

    public IReadOnlyList<FeedbackItem> Items => _items;

    public void Seed(params FeedbackItem[] items) => _items = items;

    public Task<IReadOnlyList<FeedbackItem>> LoadAsync(CancellationToken ct) =>
        ThrowOnLoad ? throw new InvalidOperationException("storage down") : Task.FromResult(_items);

    public Task RecordAsync(FeedbackItem item, CancellationToken ct)
    {
        if (ThrowOnRecord)
        {
            throw new InvalidOperationException("storage down");
        }

        _items = FeedbackLog.Add(_items, item);
        return Task.CompletedTask;
    }
}
