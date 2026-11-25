using PostAgent.Shared.Ai;
using PostAgent.Tests.Fakes;

namespace PostAgent.Tests.Shared.Ai;

public class PromptTemplateTests
{
    private const string File = "Intro line.\n\n## Part\n<!-- editable: part-one | The part -->\nFirst text.\nSecond line.\n<!-- /editable -->\n\n## Rules\nFixed rule.\n";

    [Fact]
    public void A_section_is_found_with_its_id_label_and_default()
    {
        var section = Assert.Single(PromptTemplate.Sections("x.md", File));

        Assert.Equal(("part-one", "The part", "First text.\nSecond line."), (section.Id, section.Label, section.Default));
    }

    [Fact]
    public void Rendering_without_edits_only_removes_the_markers()
    {
        var text = PromptTemplate.Render(File, null);

        Assert.DoesNotContain("<!--", text);
        Assert.Contains("## Part\nFirst text.\nSecond line.\n\n## Rules\nFixed rule.", text);
    }

    [Fact]
    public void An_edit_replaces_only_its_section()
    {
        var text = PromptTemplate.Render(File, new Dictionary<string, string> { ["part-one"] = "Own text." });

        Assert.Contains("## Part\nOwn text.\n\n## Rules\nFixed rule.", text);
        Assert.DoesNotContain("First text.", text);
    }

    [Fact]
    public void The_description_names_the_editable_sections()
    {
        var text = PromptTemplate.Describe(File, null);

        Assert.Contains("[editable section part-one: The part]\nFirst text.\nSecond line.\n[end of section]", text);
        Assert.Contains("Fixed rule.", text);
    }

    [Fact]
    public void Every_shipped_prompt_renders_without_markers_and_keeps_its_data_rule()
    {
        foreach (var name in PromptLibrary.Names)
        {
            var text = DefaultPrompts.Library.Render(name, null);

            Assert.DoesNotContain("<!--", text);
            Assert.Contains(name == "chat" ? "is data" : "data", text);
        }
    }

    [Fact]
    public void Section_ids_are_unique_and_the_safety_rules_stay_outside_the_sections()
    {
        var sections = DefaultPrompts.Library.Sections;

        Assert.Equal(sections.Count, sections.Select(s => s.Id).Distinct().Count());
        Assert.All(sections, s => Assert.DoesNotContain("data, not instructions", s.Default));
        Assert.All(sections, s => Assert.DoesNotContain("`topic`", s.Default));
    }
}
