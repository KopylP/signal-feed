using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using OpenAI;
using PostAgent.Chat.Agent;
using PostAgent.Shared.Ai;
using PostAgent.Shared.Chat;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Chat.Agent;

// The real OpenAI client over a recording HTTP handler: the fake IChatClient cannot show what OpenAI is actually sent.
public class ChatModelRequestTests
{
    private const string ResponseJson =
        """{"id":"resp_1","object":"response","created_at":1,"status":"completed","model":"gpt-6.1-terra","output":[{"type":"message","id":"msg_1","status":"completed","role":"assistant","content":[{"type":"output_text","text":"Відповідь","annotations":[]}]}],"parallel_tool_calls":true,"tool_choice":"auto","tools":[]}""";

    private static async Task<(RecordingHandler Handler, ChatReply Reply)> Ask(Action<AgentOptions>? configure = null)
    {
        var handler = new RecordingHandler();
        var settings = Options(o =>
        {
            o.OpenAIApiKey = "key";
            // The recording transport answers with plain JSON, not a stream of events.
            o.ChatStreamEnabled = false;
            configure?.Invoke(o);
        });
        // The same construction as production, with only the transport replaced.
        var chat = AiServices.CreateChatClient(settings, new OpenAIClientOptions { Transport = new HttpClientPipelineTransport(new HttpClient(handler)) });
        var options = Microsoft.Extensions.Options.Options.Create(settings);
        var state = new ChatState(new ReportSnapshot("r1", Now, "Огляд", [], []), [], null);

        var reply = await new ChatAgent(chat, new PostAgent.Tests.SettingsMenu.MenuHarness().ToolsFactory, new DefaultPrompts(), new InMemoryProfileStore(), new ChatDraftStreams(new RecordingDrafts(), new FakeTimeProvider(Now), NullLogger<ChatDraftStream>.Instance), new FakeEffectiveOptions(settings), NullLogger<ChatAgent>.Instance).AskAsync(ChatId, state, "питання", null, CancellationToken.None);
        return (handler, reply);
    }

    [Fact]
    public async Task The_request_goes_to_the_responses_api_with_tools_and_the_default_medium_reasoning()
    {
        // OpenAI rejects function tools together with reasoning on /v1/chat/completions (HTTP 400, found in production).
        var (handler, reply) = await Ask();

        Assert.Equal("Відповідь", reply.Text);
        Assert.EndsWith("/responses", handler.Path);
        Assert.Contains("\"get_post\"", handler.Body);
        Assert.Contains("\"effort\":\"medium\"", handler.Body);
        Assert.Contains("\"store\":false", handler.Body);
        Assert.Contains("reasoning.encrypted_content", handler.Body);
    }

    [Fact]
    public async Task The_reasoning_effort_and_the_output_limit_follow_the_settings()
    {
        var (handler, _) = await Ask(o =>
        {
            o.ChatReasoningEffort = "None";
            o.ChatMaxOutputTokens = 1234;
        });

        Assert.Contains("\"effort\":\"none\"", handler.Body);
        Assert.Contains("\"max_output_tokens\":1234", handler.Body);
    }

    private const string ReasoningAndToolCall =
        """{"id":"resp_1","object":"response","created_at":1,"status":"completed","model":"gpt-6.1-terra","output":[{"type":"reasoning","id":"rs_1","summary":[],"encrypted_content":"ENC-1"},{"type":"function_call","id":"fc_1","call_id":"call_1","name":"get_post","arguments":"{\"id\":\"2026-11-25/missing\"}","status":"completed"}],"parallel_tool_calls":true,"tool_choice":"auto","tools":[]}""";

    private static string Message(string text) =>
        $$"""{"id":"resp_x","object":"response","created_at":1,"status":"completed","model":"gpt-6.1-terra","output":[{"type":"message","id":"msg_x","status":"completed","role":"assistant","content":[{"type":"output_text","text":"{{text}}","annotations":[]}]}],"parallel_tool_calls":true,"tool_choice":"auto","tools":[]}""";

    [Fact]
    public async Task The_hidden_reasoning_is_sent_back_after_a_tool_call_and_in_the_next_question()
    {
        // With nothing stored at OpenAI, a function call must be followed by its reasoning item, in the same turn and after the
        // conversation went through the blob (JSON) between two questions; OpenAI answers HTTP 400 otherwise.
        var handler = new RecordingHandler(ReasoningAndToolCall, Message("Перша відповідь"), Message("Друга відповідь"));
        var settings = Options(o =>
        {
            o.OpenAIApiKey = "key";
            o.ChatStreamEnabled = false;
        });
        var chat = AiServices.CreateChatClient(settings, new OpenAIClientOptions { Transport = new HttpClientPipelineTransport(new HttpClient(handler)) });
        var options = Microsoft.Extensions.Options.Options.Create(settings);
        var agent = new ChatAgent(chat, new PostAgent.Tests.SettingsMenu.MenuHarness().ToolsFactory, new DefaultPrompts(), new InMemoryProfileStore(), new ChatDraftStreams(new RecordingDrafts(), new FakeTimeProvider(Now), NullLogger<ChatDraftStream>.Instance), new FakeEffectiveOptions(settings), NullLogger<ChatAgent>.Instance);
        var state = new ChatState(new ReportSnapshot("r1", Now, "Огляд", [], []), [], null);

        var first = await agent.AskAsync(ChatId, state, "перше", null, CancellationToken.None);
        var stored = ChatStateJson.Write(state.WithProcessed(1, first.Session));
        Assert.True(ChatStateJson.TryRead(stored, out var reloaded));
        var second = await agent.AskAsync(ChatId, reloaded, "друге", null, CancellationToken.None);

        Assert.Equal("Перша відповідь", first.Text);
        Assert.Equal("Друга відповідь", second.Text);
        Assert.Equal(3, handler.Bodies.Count);
        Assert.Contains("ENC-1", handler.Bodies[1]);          // the request that carries the tool result
        Assert.Contains("function_call_output", handler.Bodies[1]);
        Assert.Contains("ENC-1", handler.Bodies[2]);          // the next question, after the round trip through the blob
        Assert.Contains("function_call_output", handler.Bodies[2]);
    }

    private sealed class RecordingHandler(params string[] responses) : HttpMessageHandler
    {
        private int _next;

        public List<string> Bodies { get; } = [];

        public string Body { get; private set; } = string.Empty;

        public string Path { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri?.AbsolutePath ?? string.Empty;
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Bodies.Add(Body);
            var json = responses.Length == 0 ? ResponseJson : responses[Math.Min(_next++, responses.Length - 1)];
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
