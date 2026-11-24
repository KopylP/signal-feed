using PostAgent.Shared.Notifications;

namespace PostAgent.Tests.Fakes;

internal sealed class RecordingNotifier : ITelegramNotifier
{
    public List<string> Sent { get; } = [];

    // The 1-based number of the call that fails (0: none), to test what happens when a later message cannot be sent.
    public int FailOnCall { get; init; }

    public Task SendAsync(string html, CancellationToken ct)
    {
        if (Sent.Count + 1 == FailOnCall)
        {
            throw new InvalidOperationException("Telegram is down");
        }

        Sent.Add(html);
        return Task.CompletedTask;
    }
}
