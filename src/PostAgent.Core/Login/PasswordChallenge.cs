namespace PostAgent.Login;

// What Telegram gives for checking a 2FA password with SRP (account.getPassword): public values only.
// The Mini App computes the proof from them on the owner's phone, so the password itself never leaves the phone.
public sealed record PasswordChallenge(long SrpId, int G, byte[] P, byte[] Salt1, byte[] Salt2, byte[] SrpB);
