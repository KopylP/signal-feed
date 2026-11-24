namespace PostAgent.Storage;

/// <summary>
/// Одне очищення даних, що більше не потрібні. CleanupFunction виконує всі зареєстровані в DI задачі,
/// тож нове очищення (інша таблиця, черга, blob) це новий клас і один рядок у Program.cs.
/// </summary>
public interface ICleanupTask
{
    string Name { get; }

    /// <summary>Видаляє застаріле й повертає кількість видалених записів.</summary>
    Task<int> RunAsync(DateTimeOffset now, CancellationToken ct);
}
