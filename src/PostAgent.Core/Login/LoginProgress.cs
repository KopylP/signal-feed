namespace PostAgent.Login;

// Where one sign-in stands: the step Telegram expects next, the wrong codes and passwords so far, and the phone as it may be shown.
public sealed class LoginProgress
{
    public const int MaxFailures = 3;

    public LoginStep Step { get; set; } = LoginStep.PhoneRequired;

    public string MaskedPhone { get; set; } = string.Empty;

    private int Failures { get; set; }

    // True when this failure used up the last try.
    public bool CountFailure() => ++Failures >= MaxFailures;
}
