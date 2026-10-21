#if __APPLE__
using Task.Monitor.Interop.Mach;
using Task.Monitor.System.Services.Common;

namespace Task.Monitor.System.Services.LaunchdServices;

internal sealed class LaunchdScanCache
{
    private readonly Dictionary<string, (DateTime Modified, LaunchdJobDefinition? Value)> definitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (DateTime Modified, string? Value)>               publishers  = new(StringComparer.Ordinal);

    private readonly HashSet<string> seen = new(StringComparer.Ordinal);

    private T Get<T>(Dictionary<string, (DateTime Modified, T Value)> cache, string path, Func<string, T> read)
    {
        seen.Add(path);

        DateTime modified = File.GetLastWriteTimeUtc(path);

        if (cache.TryGetValue(path, out (DateTime Modified, T Value) cached) && cached.Modified == modified) {
            return cached.Value;
        }

        T value     = read(path);
        cache[path] = (modified, value);

        return value;
    }

    public LaunchdJobDefinition? GetDefinition(string plistPath) =>
        Get(definitions, plistPath, static path => LaunchdJobDefinition.Parse(PropertyList.ReadFile(path)));

    public string? GetPublisher(string programPath) =>
        Get(publishers, programPath, CodeSigningPublisher.Resolve);

    public void Prune()
    {
        Prune(definitions);
        Prune(publishers);
        seen.Clear();
    }

    private void Prune<T>(Dictionary<string, (DateTime Modified, T Value)> cache)
    {
        foreach (string path in cache.Keys) {
            if (!seen.Contains(path)) {
                cache.Remove(path);
            }
        }
    }
}
#endif
