using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Notifications.Telegram;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;

namespace PostAgent.Tests.Shared.Notifications;

public class TelegramNotifierTests
{
    // Answers like the Bot API: ok for every chat except those in "blocked", where the bot was blocked by the user.
    private sealed class FakeBotApi(params long[] blocked) : HttpMessageHandler
    {
        public List<long> Chats { get; } = [];

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            var chatId = long.Parse(System.Text.RegularExpressions.Regex.Match(body, "\"chat_id\":\\s*\"?(-?\\d+)").Groups[1].Value);
            Chats.Add(chatId);
            Bodies.Add(body);
            return blocked.Contains(chatId)
                ? Json(HttpStatusCode.Forbidden, "{\"ok\":false,\"error_code\":403,\"description\":\"Forbidden: bot was blocked by the user\"}")
                : Json(HttpStatusCode.OK, $"{{\"ok\":true,\"result\":{{\"message_id\":1,\"date\":0,\"chat\":{{\"id\":{chatId},\"type\":\"private\"}}}}}}");
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
            new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private static TelegramNotifier Notifier(FakeBotApi api, params long[] bound) =>
        new(
            new HttpClient(api),
            Options.Create(new AgentOptions { TelegramBotToken = "123456:TEST" }),
            new ChatResolver(Options.Create(new AgentOptions()), new InMemoryChatBindingStore(bound)),
            new FakeTimeProvider(),
            NullLogger<TelegramNotifier>.Instance);

    [Fact]
    public async Task A_broadcast_goes_to_every_bound_chat()
    {
        var api = new FakeBotApi();

        await Notifier(api, 42, 43).SendAsync("дайджест", CancellationToken.None);

        Assert.Equal([42L, 43L], api.Chats);
    }

    [Fact]
    public async Task A_chat_that_cannot_be_reached_does_not_keep_the_others_from_the_message()
    {
        var api = new FakeBotApi(blocked: 42);

        await Notifier(api, 42, 43).SendAsync("дайджест", CancellationToken.None);

        Assert.Equal([42L, 43L], api.Chats);
    }

    [Fact]
    public async Task A_broadcast_fails_when_no_chat_received_it()
    {
        var api = new FakeBotApi(blocked: 42);

        await Assert.ThrowsAnyAsync<Exception>(() => Notifier(api, 42).SendAsync("дайджест", CancellationToken.None));
    }

    [Fact]
    public async Task Choices_are_sent_as_a_reply_keyboard_to_the_chat_that_asked()
    {
        var api = new FakeBotApi();

        await Notifier(api, 42, 43).SendToAsync(43, "Яке джерело?", ReplyKeyboard.Of(["@one", "@two"]), CancellationToken.None);

        Assert.Equal([43L], api.Chats);
        Assert.Contains("\"keyboard\"", api.Bodies[0]);
        Assert.Contains("@two", api.Bodies[0]);
        Assert.Contains("\"one_time_keyboard\":true", api.Bodies[0]);
    }

    [Fact]
    public async Task A_message_to_one_chat_goes_only_there()
    {
        var api = new FakeBotApi();

        await Notifier(api, 42, 43).SendToAsync(43, "відповідь", CancellationToken.None);

        Assert.Equal([43L], api.Chats);
    }

    [Fact]
    public async Task A_broadcast_without_a_bound_chat_says_how_to_pair()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Notifier(new FakeBotApi()).SendAsync("дайджест", CancellationToken.None));

        Assert.Contains("/start", error.Message);
    }
}
