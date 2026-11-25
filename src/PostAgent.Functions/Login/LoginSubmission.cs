namespace PostAgent.Login;

// What the owner typed into the Mini App: the phone of the account to sign in, the code, the 2FA password. Any of them may be empty.
public sealed record LoginSubmission(string? Phone, string? Code, string? Password);
