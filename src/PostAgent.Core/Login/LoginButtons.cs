using PostAgent.Shared.Notifications;

namespace PostAgent.Login;

// The Mini App button of each stage of one sign-in: every one opens the form for its own field.
public sealed record LoginButtons(InlineKeyboard Phone, InlineKeyboard Code, InlineKeyboard Password)
{
    // Null without a valid website host name: the form cannot be opened.
    public static LoginButtons? Build(string? host, string attemptId) =>
        LoginPageUrl.Build(host, attemptId, LoginPageUrl.PhoneStage) is { } phone
        && LoginPageUrl.Build(host, attemptId, LoginPageUrl.CodeStage) is { } code
        && LoginPageUrl.Build(host, attemptId, LoginPageUrl.PasswordStage) is { } password
            ? new LoginButtons(Button(LoginTexts.PhoneButton, phone), Button(LoginTexts.CodeButton, code), Button(LoginTexts.PasswordButton, password))
            : null;

    private static InlineKeyboard Button(string text, Uri url) => new([[KeyboardButton.OpenWebApp(text, url, ButtonStyle.Primary)]]);
}
