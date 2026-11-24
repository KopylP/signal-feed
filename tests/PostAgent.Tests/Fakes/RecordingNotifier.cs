using PostAgent.Notifications;

namespace PostAgent.Tests.Fakes;

internal sealed class RecordingNotifier : ITelegramNotifier
{
    public List<string> Sent { get; } = [];

    public Task SendAsync(string html, CancellationToken ct)
    {
        Sent.Add(html);
        return Task.CompletedTask;
    }
}
