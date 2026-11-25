using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PostAgent.Login;
using TL;

namespace PostAgent.Tests.Login;

// The 2FA proof is computed by the Mini App, in JavaScript, on the owner's phone. These tests play Telegram's side of SRP:
// the verifier is first checked against WTelegram's own client implementation, then used on the page's script run under Node.
public class LoginSrpTests
{
    // The 2048-bit safe prime of RFC 3526 (group 14), with g = 2: a prime that passes WTelegram's checks.
    private const string PrimeHex =
        "FFFFFFFFFFFFFFFFC90FDAA22168C234C4C6628B80DC1CD129024E088A67CC74020BBEA63B139B22514A08798E3404DDEF9519B3CD3A431B302B0A6DF25F1437"
        + "4FE1356D6D51C245E485B576625E7EC6F44C42E9A637ED6B0BFF5CB6F406B7EDEE386BFB5A899FA5AE9F24117C4B1FE649286651ECE45B3DC2007CB8A163BF05"
        + "98DA48361C55D39A69163FA8FD24CF5F83655D23DCA3AD961C62F356208552BB9ED529077096966D670C354E4ABC9804F1746C08CA18217C32905E462E36CE3B"
        + "E39E772C180E86039B2783A2EC07A28FB5C55DF06F4C52C9DE2BCBF6955817183995497CEA956AE515D2261898FA051015728E5A8AACAA68FFFFFFFFFFFFFFFF";

    private const string Password = "Пароль 2FA 𝄞 with spaces";

    [Fact]
    public async Task The_verifier_accepts_the_proof_that_wtelegram_computes()
    {
        var server = Server.Create(Password);
        var account = new Account_Password
        {
            current_algo = new PasswordKdfAlgoSHA256SHA256PBKDF2HMACSHA512iter100000SHA256ModPow
            {
                salt1 = server.Salt1,
                salt2 = server.Salt2,
                g = server.G,
                p = server.P,
            },
            srp_B = server.SrpB,
            srp_id = 1,
        };

        var proof = await WTelegram.Client.InputCheckPassword(account, Password);

        Assert.True(server.Verify(proof.A, proof.M1));
        Assert.False(server.Verify(proof.A, SHA256.HashData(proof.M1)));
    }

    [Fact]
    public async Task The_page_script_proves_the_right_password()
    {
        var server = Server.Create(Password);

        var (a, m1) = await RunPageScriptAsync(server, Password);

        Assert.Equal(PasswordProof.ALength, a.Length);
        Assert.Equal(PasswordProof.M1Length, m1.Length);
        Assert.True(server.Verify(a, m1));
    }

    [Fact]
    public async Task The_page_script_cannot_prove_a_wrong_password()
    {
        var server = Server.Create(Password);

        var (a, m1) = await RunPageScriptAsync(server, Password + "!");

        Assert.False(server.Verify(a, m1));
    }

    private static async Task<(byte[] A, byte[] M1)> RunPageScriptAsync(Server server, string password)
    {
        var script = Path.Combine(Path.GetTempPath(), $"srp-{Guid.NewGuid():N}.mjs");
        await File.WriteAllTextAsync(script, LoginSrp.Script + """

            const challenge = JSON.parse(process.argv[2]);
            srpProof(challenge, process.argv[3]).then((proof) => console.log(JSON.stringify(proof)));
            """);
        try
        {
            var challenge = JsonSerializer.Serialize(new { g = server.G, p = server.P, salt1 = server.Salt1, salt2 = server.Salt2, srpB = server.SrpB });
            var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8 };
            start.ArgumentList.Add(script);
            start.ArgumentList.Add(challenge);
            start.ArgumentList.Add(password);

            // Node is on every GitHub-hosted runner and in the dev container; without it this test cannot check the page.
            using var node = Process.Start(start) ?? throw new InvalidOperationException("Node.js is needed to test the Mini App script.");
            var output = await node.StandardOutput.ReadToEndAsync();
            var error = await node.StandardError.ReadToEndAsync();
            await node.WaitForExitAsync();
            Assert.True(node.ExitCode == 0, error);

            using var result = JsonDocument.Parse(output);
            return (Base64(result.RootElement, "A"), Base64(result.RootElement, "M1"));
        }
        finally
        {
            File.Delete(script);
        }
    }

    private static byte[] Base64(JsonElement result, string name) =>
        Convert.FromBase64String(result.GetProperty(name).GetString() ?? throw new InvalidOperationException($"The script returned no {name}."));

    // Telegram's side: it stores v = g^x and answers with B = k*v + g^b; a proof is good when M1 matches its own computation.
    private sealed record Server(byte[] Salt1, byte[] Salt2, byte[] P, int G, BigInteger V, BigInteger B, byte[] SrpB)
    {
        private static BigInteger Prime => Big(Convert.FromHexString(PrimeHex));

        public static Server Create(string password)
        {
            var salt1 = RandomNumberGenerator.GetBytes(40);
            var salt2 = RandomNumberGenerator.GetBytes(16);
            var p = Prime;
            const int g = 2;
            var x = Big(H(salt2, Rfc2898DeriveBytes.Pbkdf2(H(salt2, H(salt1, Encoding.UTF8.GetBytes(password), salt1), salt2), salt1, 100000, HashAlgorithmName.SHA512, 64), salt2));
            var v = BigInteger.ModPow(g, x, p);
            var b = Big(RandomNumberGenerator.GetBytes(256));
            var k = Big(H(Bytes256(p), Bytes256(g)));
            var srpB = (k * v + BigInteger.ModPow(g, b, p)) % p;
            return new Server(salt1, salt2, Bytes256(p), g, v, b, Bytes256(srpB));
        }

        public bool Verify(byte[] a, byte[] m1)
        {
            var p = Prime;
            var aValue = Big(a);
            if (aValue % p == 0)
            {
                return false;
            }

            var u = Big(H(Bytes256(aValue), SrpB));
            var s = BigInteger.ModPow(aValue * BigInteger.ModPow(V, u, p) % p, B, p);
            var hp = H(P);
            var hg = H(Bytes256(G));
            var xored = hp.Select((value, i) => (byte)(value ^ hg[i])).ToArray();
            var expected = H(xored, H(Salt1), H(Salt2), Bytes256(aValue), SrpB, H(Bytes256(s)));
            return CryptographicOperations.FixedTimeEquals(expected, m1);
        }
    }

    private static byte[] H(params byte[][] parts) => SHA256.HashData(parts.SelectMany(part => part).ToArray());

    private static BigInteger Big(byte[] bytes) => new(bytes, isUnsigned: true, isBigEndian: true);

    private static byte[] Bytes256(BigInteger value)
    {
        var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        return [.. new byte[256 - bytes.Length], .. bytes];
    }
}
