using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PostAgent.Shared.Common;
using PostAgent.Shared.Settings;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace PostAgent.Pairing;

public sealed class PairingWebhook(PairingService pairing, IOptions<AgentOptions> options, ILogger<PairingWebhook> log)
{
    // A real update is a few hundred bytes; the cap keeps a flood of big bodies from filling the instance's memory.
    public const int MaxBodyBytes = 64 * 1024;

    // The body is a stream so that it is not touched until the secret token has been checked.
    public async Task<HttpStatusCode> HandleAsync(string? secretToken, Stream bodyStream, CancellationToken ct)
    {
        // Telegram sends the secret_token given to setWebhook in this header; the endpoint is otherwise public.
        if (!SecretComparison.Matches(options.Value.TelegramWebhookSecret, secretToken))
        {
            // Information, not Warning: the endpoint is public and scanners would flood telemetry.
            log.LogInformation("Webhook rejected: missing or wrong secret token");
            return HttpStatusCode.Unauthorized;
        }

        var body = await ReadBodyAsync(bodyStream, ct);
        if (body is null)
        {
            return HttpStatusCode.RequestEntityTooLarge;
        }

        Update? update;
        try
        {
            update = JsonSerializer.Deserialize<Update>(body, JsonBotAPI.Options);
        }
        catch (JsonException)
        {
            return HttpStatusCode.BadRequest;
        }

        if (update?.Message is not { } message)
        {
            return HttpStatusCode.OK;
        }

        try
        {
            await pairing.HandleAsync(message, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A 5xx makes Telegram resend the same update for a while; the user can simply send /start again.
            log.LogError(ex, "Pairing failed");
        }

        return HttpStatusCode.OK;
    }

    private static async Task<string?> ReadBodyAsync(Stream stream, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxBodyBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }
}
