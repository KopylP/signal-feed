using PostAgent.Digest;

namespace PostAgent.Tests.Digest;

public class DigestRequestTests
{
    [Fact]
    public void A_request_survives_the_queue_round_trip()
    {
        var request = new DigestRequest(42, TestData.Now.AddHours(-6), TestData.Now);

        Assert.Equal(request, DigestRequest.Parse(request.ToMessage()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"chatId\":0,\"since\":\"2026-11-24T02:00:00+00:00\",\"until\":\"2026-11-24T08:00:00+00:00\"}")]
    [InlineData("{\"chatId\":1,\"since\":\"2026-11-24T08:00:00+00:00\",\"until\":\"2026-11-24T02:00:00+00:00\"}")]
    public void A_message_that_is_not_a_complete_request_is_not_parsed(string message) =>
        Assert.Null(DigestRequest.Parse(message));
}
