using System.Security.Cryptography;
using System.Text;

namespace PostAgent.Shared.Common;

public static class SecretComparison
{
    // Hashing first gives both sides the same length, so the comparison time does not reveal how much of the secret matched.
    public static bool Matches(string expected, string? actual)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(actual))
        {
            return false;
        }

        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(actual));
        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }
}
