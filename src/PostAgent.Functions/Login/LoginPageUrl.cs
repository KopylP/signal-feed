namespace PostAgent.Login;

public static class LoginPageUrl
{
    public const string Route = "telegram/login";

    // Which form the page shows; only a hint for the page; what a submission may do is decided by LoginProcessor.
    public const string PhoneStage = "phone";
    public const string CodeStage = "code";

    // The attempt id rides in the URL so that the page can send it back; it is checked together with the signed initData.
    public static Uri? Build(string? host, string attemptId, string stage) =>
        string.IsNullOrWhiteSpace(host) || host.Contains('/') || host.Contains('?')
            ? null
            : new Uri($"https://{host}/api/{Route}?a={Uri.EscapeDataString(attemptId)}&s={stage}");
}
