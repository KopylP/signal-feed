namespace PostAgent.Cleanup;

// Одне очищення даних, що більше не потрібні. CleanupFunction виконує всі зареєстровані в DI задачі,
// тож нове очищення (інша таблиця, черга, blob) це новий клас і один рядок у CleanupServices.cs.
public interface ICleanupTask
{
    string Name { get; }

    // Видаляє застаріле й повертає кількість видалених записів.
    Task<int> RunAsync(DateTimeOffset now, CancellationToken ct);
}
