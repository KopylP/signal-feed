namespace PostAgent.Shared.Common;

public static class ListExtensions
{
    public static void AddIf<T>(this ICollection<T> list, bool condition, T item)
    {
        if (condition)
        {
            list.Add(item);
        }
    }

    public static void AddIfNotNull(this ICollection<string> list, string? item) => list.AddIf(item is not null, item!);
}
