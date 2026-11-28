using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using PostAgent.Shared.Notifications;

namespace PostAgent.Tests.Shared.Notifications;

public class TelegramResilienceTests
{
    private sealed class CountingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    private static HttpClient ClientThrough(CountingHandler handler)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("telegram")
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddStandardResilienceHandler(NotificationsServices.ConfigureTelegramResilience);

        return services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient("telegram");
    }

    [Fact]
    public async Task A_failed_send_is_not_repeated_because_a_repeat_after_a_timeout_can_duplicate_a_message()
    {
        var handler = new CountingHandler(HttpStatusCode.ServiceUnavailable);

        var response = await ClientThrough(handler).PostAsync("http://localhost/sendMessage", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, handler.Calls);
    }
}
