using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Chat;
using PostAgent.Chat.Infrastructure;
using PostAgent.Chat.Infrastructure.Tools;
using PostAgent.SettingsMenu;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using PostAgent.Tests.Shared.Settings;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PostAgent.Tests.SettingsMenu;

// Real editor, effective options and controller over in-memory fakes: only Telegram and the blob are replaced.
internal sealed class MenuHarness
{
    public const long BoundChat = 42;

    public InMemoryOverridesStore Store { get; } = new();
    public InMemoryProfileStore Profile { get; } = new();
    public FakeTimeProvider Time { get; } = new(TestData.Now);
    public RecordingPanel Panel { get; } = new();
    public AgentOptions App { get; }
    public EffectiveOptions Effective { get; }
    public SettingsEditor Editor { get; }
    public MenuController Controller { get; }
    public SettingsInbox Inbox { get; }
    public ChatResolver Resolver { get; }
    public InMemoryChatBindingStore Bindings { get; }

    // Binds more chats, as pairing does.
    public void AlsoBind(params long[] chats) => Bindings.Bound.AddRange(chats.Where(c => !Bindings.Bound.Contains(c)));
    // The posts the post tools read; set it when the test needs stored posts.
    public InMemoryPostRepository Posts { get; set; } = new();

    public ChatToolFactory ToolsFactory => new(
        new ServiceCollection()
            .AddSingleton(Editor).AddSingleton<IEffectiveOptions>(Effective).AddSingleton(DefaultPrompts.Library)
            .AddSingleton<IOverridesStore>(Store).AddSingleton<IProfileStore>(Profile)
            .AddSingleton<IPostRepository>(Posts).AddSingleton<IOptions<AgentOptions>>(Options.Create(App)).AddSingleton<TimeProvider>(Time)
            .AddChatTools().BuildServiceProvider().GetServices<IChatTool>(),
        Editor);
    public SettingsProposalSender ProposalSender => new(Editor, Effective, Panel, Time, NullLogger<SettingsProposalSender>.Instance);

    public MenuHarness(Action<AgentOptions>? configure = null, long? bound = BoundChat)
    {
        App = OverridesApplierTests.ValidApp(configure);
        var options = Options.Create(App);
        Effective = new EffectiveOptions(Store, options, Time, NullLogger<EffectiveOptions>.Instance);
        Editor = new SettingsEditor(Store, options, Effective, DefaultPrompts.Library, Profile, NullLogger<SettingsEditor>.Instance);
        Controller = new MenuController(Editor, Effective, Store, options, Panel, Time, NullLogger<MenuController>.Instance);
        Bindings = bound is { } b ? new InMemoryChatBindingStore(b) : new InMemoryChatBindingStore();
        Resolver = new ChatResolver(options, Bindings);
        Inbox = new SettingsInbox(Resolver, Store, Editor, Controller, Panel, Time, NullLogger<SettingsInbox>.Instance);
    }

    public static Update Callback(string data, int updateId = 1, long chatId = BoundChat, ChatType type = ChatType.Private, int messageId = 7) =>
        new()
        {
            Id = updateId,
            CallbackQuery = new CallbackQuery
            {
                Id = $"cb{updateId}",
                Data = data,
                Message = new Message { Id = messageId, Chat = new Telegram.Bot.Types.Chat { Id = chatId, Type = type } },
            },
        };

    public static Update Text(string text, int updateId = 1, long chatId = BoundChat, ChatType type = ChatType.Private) =>
        new() { Id = updateId, Message = new Message { Chat = new Telegram.Bot.Types.Chat { Id = chatId, Type = type }, Text = text } };

    public static IEnumerable<string> Buttons(InlineKeyboard keyboard) => keyboard.Rows.SelectMany(r => r).Select(b => b.Data);
}
