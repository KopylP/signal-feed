using System.ComponentModel.DataAnnotations;
using PostAgent.Services;

namespace PostAgent.Tests;

public class AgentOptionsTests
{
    [Fact]
    public void Sources_are_trimmed_normalized_and_deduplicated()
    {
        var options = new AgentOptions { Sources = " @News, https://t.me/chat/ ,news,, T.me/Other " };

        Assert.Equal(["news", "chat", "other"], options.SourceList);
    }

    [Fact]
    public void Empty_AlertSources_gives_an_empty_set()
    {
        Assert.Empty(new AgentOptions().AlertSourceSet);
    }

    private static AgentOptions Complete() => new()
    {
        Sources = "news",
        OpenAIApiKey = "key",
        TelegramBotToken = "token",
        TelegramChatId = "1",
        TelegramApiId = 12345,
        TelegramApiHash = "hash",
        TelegramPhone = "+380000000000",
    };

    private static List<ValidationResult> Validate(AgentOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Complete_settings_are_valid()
    {
        Assert.Empty(Validate(Complete()));
    }

    [Theory]
    [InlineData(nameof(AgentOptions.OpenAIApiKey))]
    [InlineData(nameof(AgentOptions.TelegramBotToken))]
    [InlineData(nameof(AgentOptions.TelegramApiHash))]
    [InlineData(nameof(AgentOptions.Sources))]
    public void Missing_required_setting_is_reported_by_name(string setting)
    {
        var options = Complete();
        typeof(AgentOptions).GetProperty(setting)!.SetValue(options, "");

        var error = Assert.Single(Validate(options));

        Assert.Contains(setting, error.MemberNames);
    }

    [Fact]
    public void Digest_size_above_what_fits_into_one_telegram_message_is_rejected()
    {
        var options = Complete();
        options.DigestSize = TelegramMessages.MaxItems + 1;

        Assert.Contains(nameof(AgentOptions.DigestSize), Validate(options).SelectMany(r => r.MemberNames));
    }
}
