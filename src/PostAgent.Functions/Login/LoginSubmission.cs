namespace PostAgent.Login;

// What the owner typed into the Mini App: the code, the 2FA password, or both. Either may be empty.
public sealed record LoginSubmission(string? Code, string? Password);
