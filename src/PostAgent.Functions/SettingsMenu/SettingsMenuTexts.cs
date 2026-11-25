namespace PostAgent.SettingsMenu;

// The Ukrainian texts the owner reads in the menu.
public static class SettingsMenuTexts
{
    public const string MainTitle = "⚙️ <b>Налаштування</b>";
    public const string OverridesIgnored = "⚠️ Значення з бота зараз ігноруються: разом вони недійсні. Скинь змінене в групах.";
    public const string NotOnTheList = "Цієї моделі немає в списку, вона задана в налаштуваннях застосунку й лишається, поки не обереш іншу.";
    public const string AskValue = "Надішли нове значення повідомленням.";
    public const string AskQuietHours = "Надішли години у форматі <code>23:00-07:00</code>, а щоб вимкнути, надішли <code>-</code>.";
    public const string AskSource = "Надішли <code>@username</code> каналу чи групи або посилання <code>t.me/…</code>.";
    public const string BadSourceName = "Це не схоже на ім'я каналу: потрібно 5–32 символи (латинські літери, цифри, _).";
    public const string SourceAdded = "✅ Додано. Перший збір перевірить, чи канал читається.";
    public const string SourcesLegend = "✅ читається, ⏳ ще не перевірено, 🚨 алерти.";
    public const string Saved = "✅ Збережено";
    public const string Cancelled = "Скасовано.";
}
