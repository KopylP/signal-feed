using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace PostAgent.Webhook;

public sealed class TelegramWebhookFunction(TelegramWebhook webhook)
{
    private const string SecretHeader = "X-Telegram-Bot-Api-Secret-Token";

    // Anonymous on purpose: Telegram cannot send a function key. TelegramWebhook checks the secret token header instead.
    [Function(nameof(TelegramWebhookFunction))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "telegram/webhook")] HttpRequestData request,
        CancellationToken ct)
    {
        var token = request.Headers.TryGetValues(SecretHeader, out var values) ? values.FirstOrDefault() : null;

        return request.CreateResponse(await webhook.HandleAsync(token, request.Body, ct));
    }
}
