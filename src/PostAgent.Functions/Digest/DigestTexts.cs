namespace PostAgent.Digest;

// What the reader sees after /digest. Ukrainian: it goes to the end user.
public static class DigestTexts
{
    public const string Usage =
        "Дайджест за період:\n"
        + "<code>/digest</code> — за останню добу\n"
        + "<code>/digest 6</code> — за останні 6 годин\n"
        + "<code>/digest today</code> або <code>/digest вчора</code>\n"
        + "<code>/digest 2026-11-25T18:00 2026-11-25T23:00</code> — точний період (дата без часу — вся доба)\n"
        + "Час — у вашому часовому поясі; пости зберігаються обмежений час, раніше їх уже немає.";

    public const string Failed = "Не вдалося скласти дайджест, спробуйте ще раз пізніше.";

    public static string Preparing(string period) => $"⏳ Готую дайджест за {period}…";

    public static string Empty(string period) => $"За {period} оцінених постів не знайшов.";
}
