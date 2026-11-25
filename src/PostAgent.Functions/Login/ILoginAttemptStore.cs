namespace PostAgent.Login;

// The current sign-in. Every write is conditional, so the Mini App and the waiting function never overwrite each other.
public interface ILoginAttemptStore
{
    Task<LoginAttempt?> GetAsync(CancellationToken ct);

    // False while another attempt has not expired.
    Task<bool> TryStartAsync(LoginAttempt attempt, DateTimeOffset now, CancellationToken ct);

    // Puts what the Mini App sent into the attempt with this id. False when it is gone, expired or used up its submissions.
    Task<bool> TrySubmitAsync(string attemptId, LoginSubmission submission, DateTimeOffset now, CancellationToken ct);

    // Reads and clears what was submitted, so a code or a password stays in storage only until it is used. Null when nothing is waiting.
    Task<LoginSubmission?> TakeSubmissionAsync(string attemptId, CancellationToken ct);

    // Removes the attempt with this id (not a newer one).
    Task DeleteAsync(string attemptId, CancellationToken ct);
}
