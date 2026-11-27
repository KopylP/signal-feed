using PostAgent.Chat.Memory;
using PostAgent.Tests.SettingsMenu;

namespace PostAgent.Tests.Chat.Memory;

public class ReaderMemoryTests
{
    private const long Chat = TestData.ChatId;
    private readonly MenuHarness _h = new();

    private ReaderMemory Memory => _h.ReaderMemory;

    private Task<MemoryEdit> RememberAsync(string? note, string title, string[] keywords, params string[] facts) =>
        Memory.RememberAsync(Chat, note, title, keywords, facts, CancellationToken.None);

    [Fact]
    public async Task A_new_note_is_saved_with_today_s_date_and_told_to_the_reader()
    {
        var edit = await RememberAsync(null, "Робота", ["work"], "Веде маркетплейс");

        Assert.Equal("🧠 Запам'ятав: «Робота»", edit.Notice);
        Assert.Equal(["2026-11-24-work.md"], _h.Memory.FileNames());
        Assert.Contains("- Веде маркетплейс", _h.Memory.TextOf("2026-11-24-work.md"));
    }

    [Fact]
    public async Task The_eleventh_note_is_refused_and_nothing_is_written()
    {
        for (var i = 0; i < MemoryNoteFormat.MaxNotes; i++)
        {
            _h.Memory.Seed($"2026-11-0{i % 9 + 1}-n{i}.md", "# N\n");
        }

        var edit = await RememberAsync("", "Ще одна", ["more"], "x");

        Assert.False(edit.Changed);
        Assert.Contains("memory is full", edit.Answer);
        Assert.Equal(MemoryNoteFormat.MaxNotes, _h.Memory.FileNames().Count);
    }

    [Fact]
    public async Task A_full_memory_can_still_update_a_note()
    {
        for (var i = 0; i < MemoryNoteFormat.MaxNotes; i++)
        {
            _h.Memory.Seed($"2026-11-0{i % 9 + 1}-n{i}.md", "# N\n");
        }

        var edit = await RememberAsync("2026-11-01-n0.md", "N0", ["n0"], "updated");

        Assert.True(edit.Changed);
        Assert.Equal(MemoryNoteFormat.MaxNotes, _h.Memory.FileNames().Count);
    }

    [Fact]
    public async Task Updating_a_note_replaces_it_under_today_s_date_and_new_keywords()
    {
        _h.Memory.Seed("2026-11-01-work.md", "# Old\n");

        var edit = await RememberAsync("2026-11-01-work", "Робота", ["work", "seo"], "Нове");

        Assert.Equal("🧠 Оновив нотатку: «Робота»", edit.Notice);
        Assert.Equal(["2026-11-24-work-seo.md"], _h.Memory.FileNames());
    }

    [Fact]
    public async Task Updating_a_note_of_today_with_the_same_keywords_writes_it_in_place()
    {
        _h.Memory.Seed("2026-11-24-work.md", "# Old\n");

        var edit = await RememberAsync("2026-11-24-work.md", "Робота", ["work"], "Нове");

        Assert.True(edit.Changed);
        Assert.Equal(["2026-11-24-work.md"], _h.Memory.FileNames());
        Assert.Contains("- Нове", _h.Memory.TextOf("2026-11-24-work.md"));
    }

    [Fact]
    public async Task A_new_note_that_would_take_the_name_of_another_is_sent_back_to_update_it()
    {
        _h.Memory.Seed("2026-11-24-work.md", "# Old\n");

        var edit = await RememberAsync(null, "Робота", ["work"], "Нове");

        Assert.False(edit.Changed);
        Assert.Contains("note=2026-11-24-work.md", edit.Answer);
        Assert.Equal("# Old\n", _h.Memory.TextOf("2026-11-24-work.md"));
    }

    [Fact]
    public async Task An_unknown_note_is_neither_updated_nor_created()
    {
        var edit = await RememberAsync("2026-01-01-made-up.md", "T", ["k"], "x");

        Assert.False(edit.Changed);
        Assert.Empty(_h.Memory.FileNames());
    }

    [Fact]
    public async Task A_lost_race_changes_nothing_and_is_not_told_as_done()
    {
        _h.Memory.Seed("2026-11-24-work.md", "# Old\n");
        _h.Memory.FailNextWrite = true;

        var edit = await RememberAsync("2026-11-24-work.md", "Робота", ["work"], "Нове");

        Assert.False(edit.Changed);
        Assert.Equal("# Old\n", _h.Memory.TextOf("2026-11-24-work.md"));
    }

    [Fact]
    public async Task Forgetting_removes_the_note_and_tells_its_title()
    {
        _h.Memory.Seed("2026-11-01-work.md", "# Робота\nKeywords: work\n");

        var edit = await Memory.ForgetAsync(Chat, "2026-11-01-work.md", CancellationToken.None);

        Assert.Equal("🧠 Забув: «Робота»", edit.Notice);
        Assert.Empty(_h.Memory.FileNames());
    }

    [Fact]
    public async Task A_chat_never_touches_the_notes_of_another_chat()
    {
        _h.Memory.Seed("2026-11-01-work.md", "# Owner\n", Chat);

        var forget = await Memory.ForgetAsync(77, "2026-11-01-work.md", CancellationToken.None);
        var listed = await Memory.ListAsync(77, CancellationToken.None);

        Assert.False(forget.Changed);
        Assert.Empty(listed);
        Assert.Equal(["2026-11-01-work.md"], _h.Memory.FileNames(Chat));
    }

    [Fact]
    public async Task The_old_profile_becomes_the_first_note_once_and_is_removed()
    {
        _h.Memory.SeedLegacyProfile("I run a catalog.\n\nI like numbers.", new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero));

        var notes = await Memory.ListAsync(Chat, CancellationToken.None);

        var note = Assert.Single(notes).Note;
        Assert.Equal("2026-11-20-about-me.md", note.FileName);
        Assert.Equal("# Про мене\nKeywords: about-me\nUpdated: 2026-11-20\n\n- I run a catalog.\n- I like numbers.\n", note.Text);
        Assert.False(_h.Memory.HasLegacyProfile());
    }

    [Fact]
    public async Task An_old_profile_is_left_alone_while_the_memory_has_notes()
    {
        _h.Memory.Seed("2026-11-24-work.md", "# Work\n");
        _h.Memory.SeedLegacyProfile("I run a catalog.", TestData.Now);

        await Memory.ListAsync(Chat, CancellationToken.None);

        Assert.True(_h.Memory.HasLegacyProfile());
        Assert.Single(_h.Memory.FileNames());
    }

    [Fact]
    public async Task A_failure_to_move_the_old_profile_does_not_fail_the_question()
    {
        _h.Memory.ThrowOnLegacyProfile = true;

        var notes = await Memory.ListAsync(Chat, CancellationToken.None);

        Assert.Empty(notes);
    }
}
