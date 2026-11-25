namespace PostAgent.Login;

public static class LoginPageUrl
{
    public const string Route = "telegram/login";

    // The attempt id rides in the URL so that the page can send it back; it is checked together with the signed initData.
    public static Uri? Build(string? host, string attemptId) =>
        string.IsNullOrWhiteSpace(host) || host.Contains('/') || host.Contains('?')
            ? null
            : new Uri($"https://{host}/api/{Route}?a={Uri.EscapeDataString(attemptId)}");
}
