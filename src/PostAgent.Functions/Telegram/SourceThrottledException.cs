namespace PostAgent.Telegram;

/// <summary>Telegram просить зачекати довше, ніж ми готові (FLOOD_WAIT): джерело читаємо в наступному запуску.</summary>
public sealed class SourceThrottledException(string message, Exception inner) : Exception(message, inner);
