using PostAgent.Shared.Posts;

namespace PostAgent.Shared.Alerts;

// Chooses the posts to alert about and publishes only those to to-alert (ADR-0024); the sender does not choose again.
// Shared so that the scorer and topic following can ask it without depending on the Alerts feature.
public interface IAlertDecider
{
    // After a batch was scored and saved: important or bursting group posts and followed topics.
    Task DecideAsync(IReadOnlyList<PostEntity> scored, CancellationToken ct);

    // After a topic started to be followed: its matches among the recent posts that nobody announced yet.
    Task DecideTopicAsync(string phrase, float[] vector, CancellationToken ct);
}
