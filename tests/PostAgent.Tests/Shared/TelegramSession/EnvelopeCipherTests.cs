using System.Security.Cryptography;
using PostAgent.Shared.TelegramSession;
using PostAgent.Tests.Fakes;

namespace PostAgent.Tests.Shared.TelegramSession;

public class EnvelopeCipherTests
{
    private readonly FakeKeyWrapper _vault = new();

    [Fact]
    public async Task Round_trips_with_one_wrap_per_process_and_no_unwrap_for_its_own_blobs()
    {
        var cipher = new EnvelopeCipher(_vault);

        var first = await cipher.ProtectAsync([1, 2, 3], "p", CancellationToken.None);
        var second = cipher.Protect([4, 5], "p");

        Assert.Equal([1, 2, 3], await new EnvelopeCipher(_vault).UnprotectAsync(first, "p", CancellationToken.None));
        Assert.Equal([4, 5], await new EnvelopeCipher(_vault).UnprotectAsync(second, "p", CancellationToken.None));
        Assert.Equal(1, _vault.Wraps);
    }

    [Fact]
    public async Task Unwraps_a_data_key_once_however_often_the_blob_is_read()
    {
        var sealedBlob = await new EnvelopeCipher(_vault).ProtectAsync([7], "p", CancellationToken.None);
        var reader = new EnvelopeCipher(_vault);

        for (var i = 0; i < 5; i++)
        {
            await reader.UnprotectAsync(sealedBlob, "p", CancellationToken.None);
        }

        Assert.Equal(1, _vault.Unwraps);
    }

    [Fact]
    public async Task Writes_with_its_own_key_so_a_rotated_key_version_takes_over()
    {
        var oldBlob = await new EnvelopeCipher(_vault).ProtectAsync([7], "p", CancellationToken.None);
        _vault.KeyId = FakeKeyWrapper.RotatedKeyId;
        var session = new EnvelopeCipher(_vault);
        await session.UnprotectAsync(oldBlob, "p", CancellationToken.None);

        var rewritten = session.Protect([8], "p");

        Assert.Equal(FakeKeyWrapper.RotatedKeyId, SessionEnvelope.ReadKey(rewritten).KeyId);
        Assert.Equal([7], await session.UnprotectAsync(oldBlob, "p", CancellationToken.None));
    }

    [Fact]
    public async Task Refuses_a_blob_in_clear()
    {
        await Assert.ThrowsAnyAsync<CryptographicException>(() => new EnvelopeCipher(_vault).UnprotectAsync([1, 2, 3], "p", CancellationToken.None));
    }
}
