namespace PostAgent.Login;

// The SRP proof of the 2FA password (A and M1 of auth.checkPassword). It is good for one challenge only,
// and the password cannot be recovered from it.
public sealed record PasswordProof(byte[] A, byte[] M1)
{
    public const int ALength = 256;
    public const int M1Length = 32;
}
