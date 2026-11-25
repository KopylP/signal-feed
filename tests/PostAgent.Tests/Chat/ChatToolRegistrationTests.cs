using Microsoft.Extensions.AI;
using PostAgent.Chat.Infrastructure.Tools;
using PostAgent.Tests.SettingsMenu;

namespace PostAgent.Tests.Chat;

public class ChatToolRegistrationTests
{
    private static IReadOnlyList<AIFunction> Functions() => new MenuHarness().ToolsFactory.Create([], TestData.ChatId);

    [Fact]
    public void Every_tool_class_in_the_assembly_is_a_tool_of_the_agent()
    {
        var classes = typeof(IChatTool).Assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && t.IsAssignableTo(typeof(IChatTool)));

        Assert.Equal(classes.Count(), Functions().Count);
    }

    [Fact]
    public void Tool_names_are_unique_and_snake_case()
    {
        var names = Functions().Select(f => f.Name).ToList();

        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(names, n => Assert.Matches("^[a-z]+(_[a-z]+)+$", n));
    }

    [Fact]
    public void A_tool_tells_the_model_what_its_parameters_mean()
    {
        var edit = Functions().Single(f => f.Name == "propose_prompt_edit");
        var properties = edit.JsonSchema.GetProperty("properties");

        Assert.Equal("The fragment to replace, copied exactly.", properties.GetProperty("old_text").GetProperty("description").GetString());
        Assert.False(properties.TryGetProperty("ct", out _));
        Assert.False(string.IsNullOrWhiteSpace(edit.Description));
    }

    [Fact]
    public void Every_tool_and_every_parameter_has_a_description()
    {
        foreach (var tool in Functions())
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Description), tool.Name);
            if (tool.JsonSchema.TryGetProperty("properties", out var properties))
            {
                foreach (var property in properties.EnumerateObject())
                {
                    Assert.True(property.Value.TryGetProperty("description", out _), $"{tool.Name}.{property.Name}");
                    Assert.NotEqual("ct", property.Name);
                }
            }
        }
    }
}
