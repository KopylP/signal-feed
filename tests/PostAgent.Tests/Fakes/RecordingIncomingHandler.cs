using PostAgent.Shared.Notifications;
using Telegram.Bot.Types;

namespace PostAgent.Tests.Fakes;

internal sealed class RecordingIncomingHandler : IIncomingMessageHandler
{
    public List<Update> Handled { get; } = [];

    public bool Fail { get; init; }

    public Task HandleAsync(Update update, CancellationToken ct)
    {
        Handled.Add(update);
        return Fail ? throw new InvalidOperationException("handler is down") : Task.CompletedTask;
    }
}
