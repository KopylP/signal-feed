using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PostAgent.Shared.Common;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace PostAgent.Webhook;

// The bot's single entry point: checks the secret, parses the update and offers it to the handlers. It does no work of its own.
public sealed class TelegramWebhook(
    IEnumerable<IIncomingMessageHandler> handlers,
    IOptions<AgentOptions> options,
    ILogger<TelegramWebhook> log)
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
            update = JsonSerializer.Deserialize<Update>(body.AsSpan(), JsonBotAPI.Options);
        }
        catch (JsonException)
        {
            return HttpStatusCode.BadRequest;
        }

        if (update is null || (update.Message is null && update.CallbackQuery is null))
        {
            return HttpStatusCode.OK;
        }

        try
        {
            // The secret token is already checked, so the update is from Telegram. The handlers are asked in
            // registration order and the first that claims the update wins (pairing is registered first).
            foreach (var handler in handlers)
            {
                if (await handler.HandleAsync(update, ct))
                {
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A 5xx makes Telegram resend the same update for a while; the user can simply send the message again.
            log.LogError(ex, "Handling the Telegram update failed");
        }

        return HttpStatusCode.OK;
    }

    private static async Task<byte[]?> ReadBodyAsync(Stream stream, CancellationToken ct)
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

        var bytes = buffer.ToArray();
        return bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble) ? bytes[Encoding.UTF8.Preamble.Length..] : bytes;
    }
}
