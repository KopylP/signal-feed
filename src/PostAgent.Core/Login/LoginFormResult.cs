using System.Net;

namespace PostAgent.Login;

// The answer to the Mini App: a status, and for the "challenge" action the public SRP values the page needs.
public sealed record LoginFormResult(HttpStatusCode Status, PasswordChallenge? Challenge = null)
{
    public static implicit operator LoginFormResult(HttpStatusCode status) => new(status);
}
