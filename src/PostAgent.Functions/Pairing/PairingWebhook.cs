using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PostAgent.Shared.Common;
using PostAgent.Shared.Settings;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace PostAgent.Pairing;

public sealed class PairingWebhook(PairingService pairing, IOptions<AgentOptions> options, ILogger<PairingWebhook> log)
{
    public async Task<HttpStatusCode> HandleAsync(string? secretToken, string body, CancellationToken ct)
    {
        // Telegram sends the secret_token given to setWebhook in this header; the endpoint is otherwise public.
        if (!SecretComparison.Matches(options.Value.TelegramWebhookSecret, secretToken))
        {
            log.LogWarning("Webhook rejected: missing or wrong secret token");
            return HttpStatusCode.Unauthorized;
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
}
