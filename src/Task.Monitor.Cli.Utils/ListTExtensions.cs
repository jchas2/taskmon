namespace Task.Monitor.Cli.Utils;

public static class ListTExtensions
{
    public static void AddIfMissing<T>(this List<T> list, T item)
    {
        if (!list.Contains(item)) {
            list.Add(item);
        } 
    }
}