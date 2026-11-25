using PostAgent.Login;

namespace PostAgent.Tests.Fakes;

internal sealed class InMemoryLoginAttemptStore : ILoginAttemptStore
{
    public LoginAttempt? Current { get; set; }

    public List<string> Deleted { get; } = [];

    public Task<LoginAttempt?> GetAsync(CancellationToken ct) => Task.FromResult(Current);

    public Task<bool> TryStartAsync(LoginAttempt attempt, DateTimeOffset now, CancellationToken ct)
    {
        if (Current is { } existing && !existing.IsExpired(now))
        {
            return Task.FromResult(false);
        }

        Current = attempt;
        return Task.FromResult(true);
    }

    public Task<bool> TrySubmitAsync(string attemptId, LoginSubmission submission, DateTimeOffset now, CancellationToken ct)
    {
        if (Current is not { } attempt || attempt.Id != attemptId || attempt.IsExpired(now) || attempt.Submissions >= LoginAttempt.MaxSubmissions)
        {
            return Task.FromResult(false);
        }

        Current = attempt with { Phone = submission.Phone, Code = submission.Code, Proof = submission.Proof, Submissions = attempt.Submissions + 1 };
        return Task.FromResult(true);
    }

    public Task<LoginSubmission?> TakeSubmissionAsync(string attemptId, CancellationToken ct)
    {
        // A queued form arrives at the next poll, as if the owner typed it while the function waited.
        if (Current is { } attempt && attempt.Id == attemptId && Queued.TryDequeue(out var next))
        {
            Current = attempt with { Submissions = attempt.Submissions + 1 };
            return Task.FromResult<LoginSubmission?>(next);
        }

        return Task.FromResult<LoginSubmission?>(null);
    }

    public List<PasswordChallenge> Challenges { get; } = [];

    public Task SetChallengeAsync(string attemptId, PasswordChallenge challenge, CancellationToken ct)
    {
        Challenges.Add(challenge);
        if (Current?.Id == attemptId)
        {
            Current = Current with { Challenge = challenge };
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string attemptId, CancellationToken ct)
    {
        Deleted.Add(attemptId);
        if (Current?.Id == attemptId)
        {
            Current = null;
        }

        return Task.CompletedTask;
    }

    // Forms the processor receives one per poll, in order.
    public Queue<LoginSubmission> Queued { get; } = new();
}

// Answers each step from a script: the code and the password proof that Telegram accepts, and what the first step returns.
internal sealed class ScriptedUserLogin : IUserLogin
{
    public static readonly PasswordProof GoodProof = new(new byte[256], new byte[32]);
    public static readonly PasswordProof BadProof = new(new byte[256], Enumerable.Repeat((byte)1, 32).ToArray());

    public LoginStep FirstStep { get; init; } = LoginStep.CodeRequired;
    public string Code { get; init; } = "12345";
    public bool HasPassword { get; init; }
    public bool UnsupportedPassword { get; init; }
    public Exception? StartFailure { get; init; }

    public List<string> Phones { get; } = [];
    public List<string> Codes { get; } = [];
    public List<PasswordProof> Proofs { get; } = [];
    public int ChallengesIssued { get; private set; }
    public bool Disposed { get; private set; }
    public bool Exported { get; private set; }

    public string? AccountName { get; private set; }

    public Task<LoginStep> StartAsync(string phone, CancellationToken ct)
    {
        Phones.Add(phone);
        return StartFailure is null ? Task.FromResult(FirstStep) : Task.FromException<LoginStep>(StartFailure);
    }

    public Task<LoginStep> SubmitCodeAsync(string code, CancellationToken ct)
    {
        Codes.Add(code);
        if (code != Code)
        {
            return Task.FromResult(LoginStep.CodeInvalid);
        }

        return Task.FromResult(HasPassword ? LoginStep.PasswordRequired : Finish());
    }

    public Task<PasswordChallenge?> GetPasswordChallengeAsync(CancellationToken ct)
    {
        ChallengesIssued++;
        return Task.FromResult(UnsupportedPassword
            ? null
            : new PasswordChallenge(ChallengesIssued, 3, new byte[256], [1], [2], [(byte)ChallengesIssued]));
    }

    public Task<LoginStep> SubmitPasswordProofAsync(PasswordProof proof, CancellationToken ct)
    {
        Proofs.Add(proof);
        return Task.FromResult(proof.M1.SequenceEqual(GoodProof.M1) ? Finish() : LoginStep.PasswordInvalid);
    }

    public Task<byte[]> ExportSessionAsync(CancellationToken ct)
    {
        Exported = true;
        return Task.FromResult<byte[]>([1, 2, 3]);
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }

    private LoginStep Finish()
    {
        AccountName = "reader";
        return LoginStep.Done;
    }
}

internal sealed class ScriptedUserLoginFactory(ScriptedUserLogin login) : IUserLoginFactory
{
    public int Created { get; private set; }

    public IUserLogin Create()
    {
        Created++;
        return login;
    }
}

internal sealed class RecordingSessionStore : ISessionStore
{
    public List<byte[]> Saved { get; } = [];

    public Task SaveAsync(byte[] session, CancellationToken ct)
    {
        Saved.Add(session);
        return Task.CompletedTask;
    }
}

internal sealed class RecordingLoginQueue : ILoginQueue
{
    public List<LoginRequest> Enqueued { get; } = [];
    public Exception? Failure { get; init; }

    public Task EnqueueAsync(LoginRequest request, CancellationToken ct)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        Enqueued.Add(request);
        return Task.CompletedTask;
    }
}
