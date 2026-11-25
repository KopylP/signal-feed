using PostAgent.Chat;

namespace PostAgent.Tests.Chat;

public class ChatRequestTests
{
    [Fact]
    public void A_request_survives_the_queue_round_trip()
    {
        var request = new ChatRequest(123, "Розкажи про пункт 3", "20261125070000");

        Assert.Equal(request, ChatRequest.Parse(request.ToMessage()));
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
}
