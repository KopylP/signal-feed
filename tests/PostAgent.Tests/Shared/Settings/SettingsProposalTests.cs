using PostAgent.Shared.Settings;
using PostAgent.Tests.SettingsMenu;

namespace PostAgent.Tests.Shared.Settings;

public class SettingsProposalTests
{
    private readonly MenuHarness _h = new();

    private DateTimeOffset Now => _h.Time.GetUtcNow();

    private SettingsProposal Proposal(string id, params SettingChange[] changes) => new(id, changes, Now + SettingsProposal.Lifetime);

    private static SettingChange Set(string key, string value) => new(SettingChangeKind.Set, key, value);

    [Fact]
    public async Task Applying_writes_all_the_changes_together_and_removes_the_proposal()
    {
        await _h.Editor.ProposeAsync(Proposal("1", Set("DigestSize", "8"), new SettingChange(SettingChangeKind.AddSource, "fresh")), CancellationToken.None);

        var outcome = await _h.Editor.ApplyProposalAsync("1", TestData.ChatId, Now, CancellationToken.None);

        Assert.Equal(ProposalStatus.Applied, outcome.Status);
        Assert.Equal("8", _h.Store.Current?.Values["DigestSize"]);
        Assert.Contains(_h.Store.Current!.Sources!, s => s.Name == "fresh" && !s.Verified);
        Assert.Empty(_h.Store.Current.Proposals!);
    }

    [Fact]
    public async Task A_second_apply_finds_nothing_and_changes_nothing()
    {
        await _h.Editor.ProposeAsync(Proposal("1", Set("DigestSize", "8")), CancellationToken.None);
        await _h.Editor.ApplyProposalAsync("1", TestData.ChatId, Now, CancellationToken.None);
        await _h.Editor.SetValueAsync("DigestSize", "5", CancellationToken.None);

        var outcome = await _h.Editor.ApplyProposalAsync("1", TestData.ChatId, Now, CancellationToken.None);

        Assert.Equal(ProposalStatus.Gone, outcome.Status);
        Assert.Equal("5", _h.Store.Current?.Values["DigestSize"]);
    }

    [Fact]
    public async Task An_expired_proposal_is_not_applied()
    {
        await _h.Editor.ProposeAsync(Proposal("1", Set("DigestSize", "8")), CancellationToken.None);
        _h.Time.Advance(SettingsProposal.Lifetime + TimeSpan.FromMinutes(1));

        var outcome = await _h.Editor.ApplyProposalAsync("1", TestData.ChatId, Now, CancellationToken.None);

        Assert.Equal(ProposalStatus.Expired, outcome.Status);
        Assert.Empty(_h.Store.Current!.Values);
    }

    [Fact]
    public async Task A_plan_that_is_invalid_as_a_whole_changes_nothing()
    {
        // Each step is fine on its own; together the alert threshold is above what the options allow after the other one.
        await _h.Editor.ProposeAsync(Proposal("1", Set("DigestSize", "8"), Set("MinPostChars", "5000"), Set("MaxPostChars", "100")), CancellationToken.None);

        var outcome = await _h.Editor.ApplyProposalAsync("1", TestData.ChatId, Now, CancellationToken.None);

        Assert.Equal(ProposalStatus.Failed, outcome.Status);
        Assert.Empty(_h.Store.Current!.Values);
    }

    [Fact]
    public async Task Declining_removes_the_proposal_once()
    {
        await _h.Editor.ProposeAsync(Proposal("1", Set("DigestSize", "8")), CancellationToken.None);

        Assert.True(await _h.Editor.DeclineProposalAsync("1", TestData.ChatId, CancellationToken.None));
        Assert.False(await _h.Editor.DeclineProposalAsync("1", TestData.ChatId, CancellationToken.None));
        Assert.Equal(ProposalStatus.Gone, (await _h.Editor.ApplyProposalAsync("1", TestData.ChatId, Now, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Only_the_last_five_proposals_are_kept_and_the_same_id_replaces_its_proposal()
    {
        for (var i = 1; i <= 7; i++)
        {
            await _h.Editor.ProposeAsync(Proposal(i.ToString(), Set("DigestSize", "8")), CancellationToken.None);
        }

        await _h.Editor.ProposeAsync(Proposal("7", Set("DigestSize", "9")), CancellationToken.None);

        Assert.Equal(["3", "4", "5", "6", "7"], _h.Store.Current!.Proposals!.Select(p => p.Id));
        Assert.Equal("9", _h.Store.Current.Proposals![^1].Changes[0].Value);
    }

    [Fact]
    public async Task The_dry_run_saves_nothing_and_reports_the_problem()
    {
        var bad = await _h.Editor.CheckAsync([Set("DigestSize", "9999")], TestData.ChatId, CancellationToken.None);
        var good = await _h.Editor.CheckAsync([Set("DigestSize", "8")], TestData.ChatId, CancellationToken.None);

        Assert.False(bad.Ok);
        Assert.True(good.Ok);
        Assert.Null(_h.Store.Current);
    }

    [Fact]
    public void The_message_is_built_from_the_changes_with_the_old_and_the_new_value_and_is_escaped()
    {
        var proposal = Proposal("1", Set("DigestSize", "8"), Set("AlertTimeZone", "<b>x</b>"), new SettingChange(SettingChangeKind.RemoveSource, "chat"));

        var html = SettingsProposalMessage.Proposed(proposal, _h.App);

        Assert.Contains($"{_h.App.DigestSize} → <b>8</b>", html);
        Assert.Contains("&lt;b&gt;x&lt;/b&gt;", html);
        Assert.DoesNotContain("<b>x</b>", html);
        Assert.Contains("Видалити джерело <b>@chat</b>", html);
        Assert.Equal(["pa:1", "pn:1"], SettingsProposalMessage.Keyboard(proposal).Rows.SelectMany(r => r).Select(b => b.Data));
    }

    [Fact]
    public async Task A_proposal_can_be_applied_only_from_the_chat_that_got_it()
    {
        await _h.Editor.ProposeAsync(Proposal("1", Set("DigestSize", "8")) with { ChatId = 42 }, CancellationToken.None);

        Assert.Equal(ProposalStatus.Gone, (await _h.Editor.ApplyProposalAsync("1", 99, Now, CancellationToken.None)).Status);
        Assert.False(await _h.Editor.DeclineProposalAsync("1", 99, CancellationToken.None));
        Assert.Single(_h.Store.Current!.Proposals!);
        Assert.Equal(ProposalStatus.Applied, (await _h.Editor.ApplyProposalAsync("1", 42, Now, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task A_proposal_stored_before_chats_were_recorded_can_be_applied_from_any_bound_chat()
    {
        await _h.Editor.ProposeAsync(Proposal("1", Set("DigestSize", "8")), CancellationToken.None);

        Assert.Equal(ProposalStatus.Applied, (await _h.Editor.ApplyProposalAsync("1", 99, Now, CancellationToken.None)).Status);
    }
}
