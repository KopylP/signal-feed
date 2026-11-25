namespace PostAgent.Login;

// What the Mini App sent: the phone of the account to sign in, the code, or the SRP proof of the 2FA password. Never the password.
public sealed record LoginSubmission(string? Phone, string? Code, PasswordProof? Proof);
