namespace PostAgent.Models;

/// <summary>Відповідь моделі на підсумок дайджесту: огляд дня і пояснення для кожного посту.</summary>
public sealed record DigestSummary(string Overview, List<ItemInsight> Items);

/// <summary>Що саме цікаве в пості з даним id.</summary>
public sealed record ItemInsight(string Id, string Insight);
