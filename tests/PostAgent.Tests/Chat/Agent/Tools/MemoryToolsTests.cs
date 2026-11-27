using PostAgent.Shared.Settings;
using PostAgent.Tests.SettingsMenu;

namespace PostAgent.Tests.Chat.Agent.Tools;

public class MemoryToolsTests
{
    private readonly MenuHarness _h = new();
    private readonly List<SettingChange> _draft = [];

    [Fact]
    public async Task Save_memory_writes_at_once_with_no_plan_and_records_the_line_for_the_reader()
    {
        var turn = _h.Turn(_draft, TestData.ChatId);
        var tools = new ToolShim(_h.ToolsFactory.Create(turn));

        var answer = await tools.SaveMemory(null, "Робота", ["work"], ["Веде маркетплейс"], CancellationToken.None);

        Assert.StartsWith("Saved as 2026-11-24-work.md", answer);
        Assert.Empty(_draft);
        Assert.Equal(["🧠 Запам'ятав: «Робота»"], turn.MemoryNotices);
    }

    [Fact]
    public async Task Forget_memory_removes_the_note_of_its_own_chat_only()
    {
        _h.Memory.Seed("2026-11-24-work.md", "# Робота\n", 77);
        var turn = _h.Turn(_draft, TestData.ChatId);

        var answer = await new ToolShim(_h.ToolsFactory.Create(turn)).ForgetMemory("2026-11-24-work.md", CancellationToken.None);

        Assert.StartsWith("There is no such note", answer);
        Assert.Empty(turn.MemoryNotices);
        Assert.Single(_h.Memory.FileNames(77));
    }

    [Fact]
    public async Task A_refused_note_tells_the_model_why_and_records_nothing()
    {
        var turn = _h.Turn(_draft, TestData.ChatId);

        var answer = await new ToolShim(_h.ToolsFactory.Create(turn)).SaveMemory(null, "", ["work"], ["x"], CancellationToken.None);

        Assert.StartsWith("Not saved", answer);
        Assert.Empty(turn.MemoryNotices);
        Assert.Empty(_h.Memory.FileNames());
    }
}
