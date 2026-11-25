using PostAgent.Shared.Notifications;
using Telegram.Bot.Types;

namespace PostAgent.Tests.Fakes;

internal sealed class RecordingIncomingHandler : IIncomingMessageHandler
{
    public List<Update> Handled { get; } = [];

    public bool Fail { get; init; }

    // What HandleAsync returns: whether this handler claims the update.
    public bool Claims { get; init; } = true;

    public Task<bool> HandleAsync(Update update, CancellationToken ct)
    {
        Handled.Add(update);
        return Fail ? throw new InvalidOperationException("handler is down") : Task.FromResult(Claims);
    }
}
