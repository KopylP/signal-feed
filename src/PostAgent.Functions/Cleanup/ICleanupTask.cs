using PostAgent.Shared.Cleanup;

namespace PostAgent.Cleanup;

// Одне очищення даних, що більше не потрібні. CleanupFunction виконує всі зареєстровані в DI задачі після того, як
// DailySummaryJob записав підсумки днів, тож нове очищення (інша таблиця, черга, blob) це новий клас і один рядок у CleanupServices.cs.
public interface ICleanupTask
{
    string Name { get; }

    // Видаляє застаріле й повертає кількість видалених записів. Дні після request.SummarizedThrough чіпати не можна.
    Task<int> RunAsync(CleanupRequest request, DateTimeOffset now, CancellationToken ct);
}
