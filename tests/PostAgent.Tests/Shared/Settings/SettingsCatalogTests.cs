using PostAgent.Shared.Settings;

namespace PostAgent.Tests.Shared.Settings;

public class SettingsCatalogTests
{
    private static readonly string[] NotEditable =
    [
        "OpenAIApiKey", "TelegramBotToken", "TelegramApiHash", "TelegramPairingKey", "TelegramWebhookSecret", "TelegramChatId",
        "TelegramApiId", "SessionBlobContainer", "ManualRunsEnabled", "RetentionDays", "FirstRunLookbackHours",
    ];

    public static TheoryData<string> Keys()
    {
        var data = new TheoryData<string>();
        foreach (var definition in SettingsCatalog.All)
        {
            data.Add(definition.Key);
        }
        return data;
    }

    [Fact]
    public void Reads_the_int_bounds_from_the_range_attributes()
    {
        var digestSize = SettingsCatalog.Find("DigestSize");
        var threshold = SettingsCatalog.Find("AlertThreshold");

        Assert.Equal((1, AgentOptions.MaxDigestItems), (digestSize?.Min, digestSize?.Max));
        Assert.Equal((0, 10), (threshold?.Min, threshold?.Max));
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public void Every_catalog_key_is_a_property_of_AgentOptions(string key)
    {
        Assert.NotNull(typeof(AgentOptions).GetProperty(key));
    }

    [Fact]
    public void Catalog_has_no_secret_or_infrastructure_key()
    {
        Assert.Empty(SettingsCatalog.All.Select(d => d.Key).Intersect(NotEditable));
    }

    [Theory]
    [InlineData("OpenAIModel")]
    [InlineData("OpenAIChatModel")]
    public void The_model_settings_are_choices_of_the_three_listed_models(string key)
    {
        var definition = SettingsCatalog.Find(key);

        Assert.Equal(SettingKind.Choice, definition?.Kind);
        Assert.Equal(["gpt-5.4-nano", "gpt-6.1-terra", "gpt-6.1-sol"], definition?.Choices.Select(c => c.Value));
    }

    [Fact]
    public void Reasoning_effort_is_a_choice_of_four_levels()
    {
        Assert.Equal(["none", "low", "medium", "high"], SettingsCatalog.Find("ChatReasoningEffort")?.Choices.Select(c => c.Value));
    }

    [Fact]
    public void Every_group_except_sources_has_parameters()
    {
        foreach (var group in Enum.GetValues<SettingGroup>().Where(g => g != SettingGroup.Sources))
        {
            Assert.NotEmpty(SettingsCatalog.InGroup(group));
        }
    }
}
