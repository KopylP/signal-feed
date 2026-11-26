using PostAgent.Chat.Intake;

namespace PostAgent.Tests.Chat.Intake;

public class ChatRequestTests
{
    [Fact]
    public void A_request_survives_the_queue_round_trip()
    {
        var request = new ChatRequest(123, "Розкажи про пункт 3", "20261125070000", TestData.ChatId);

        Assert.Equal(request, ChatRequest.Parse(request.ToMessage()));
    }

    [Fact]
    public void The_time_the_reader_wrote_survives_the_queue_and_may_be_absent()
    {
        var sent = new DateTimeOffset(2026, 11, 25, 7, 30, 0, TimeSpan.Zero);
        var request = new ChatRequest(123, "питання", "r1", TestData.ChatId, sent);

        Assert.Equal(sent, ChatRequest.Parse(request.ToMessage())!.SentAt);
        Assert.Null(ChatRequest.Parse("{\"updateId\":1,\"text\":\"q\",\"reportId\":\"r\",\"chatId\":5}")!.SentAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""{"updateId":1}""")]
    [InlineData("""{"text":"питання","reportId":"r1"}""")]
    public void A_malformed_queue_message_does_not_parse(string message)
    {
        Assert.Null(ChatRequest.Parse(message));
    }

    [Fact]
    public void A_message_without_a_chat_does_not_parse()
    {
        Assert.Null(ChatRequest.Parse("""{"updateId":1,"text":"питання","reportId":"r1"}"""));
    }
}
