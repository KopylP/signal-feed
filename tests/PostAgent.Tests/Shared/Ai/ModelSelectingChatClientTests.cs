using Microsoft.Extensions.AI;
using PostAgent.Shared.Ai;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;

namespace PostAgent.Tests.Shared.Ai;

public class ModelSelectingChatClientTests
{
    private static readonly ChatMessage[] Question = [new(ChatRole.User, "q")];

    [Fact]
    public async Task Sets_the_model_id_from_the_effective_options_on_every_call()
    {
        var inner = new FakeChatClient("{}");
        var effective = new FakeEffectiveOptions(TestData.Options(o => o.OpenAIModel = "gpt-5.4-nano"));
        var client = new ModelSelectingChatClient(inner, effective, o => o.OpenAIModel);

        await client.GetResponseAsync(Question);
        Assert.Equal("gpt-5.4-nano", inner.Options?.ModelId);

        effective.Current = TestData.Options(o => o.OpenAIModel = "gpt-6.1-sol");
        await client.GetResponseAsync(Question);
        Assert.Equal("gpt-6.1-sol", inner.Options?.ModelId);
    }

    [Fact]
    public async Task Does_not_change_the_callers_options_object()
    {
        var inner = new FakeChatClient("{}");
        var client = new ModelSelectingChatClient(inner, new FakeEffectiveOptions(TestData.Options()), o => o.OpenAIChatModel);
        var callers = new ChatOptions { MaxOutputTokens = 50 };

        await client.GetResponseAsync(Question, callers);

        Assert.Null(callers.ModelId);
        Assert.Equal(50, inner.Options?.MaxOutputTokens);
        Assert.Equal(TestData.Options().OpenAIChatModel, inner.Options?.ModelId);
    }
}
