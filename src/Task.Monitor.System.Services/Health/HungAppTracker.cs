namespace Task.Monitor.System.Services.Health;

internal sealed class HungAppTracker
{
    private readonly Dictionary<int, DateTime> firstSeenUtc = new();

    public IReadOnlyList<HungApp> Update(IReadOnlyCollection<int> hungPids, Func<int, string> nameOf, DateTime utcNow)
    {
        foreach (int recovered in firstSeenUtc.Keys.Except(hungPids).ToList()) {
            firstSeenUtc.Remove(recovered);
        }

        foreach (int pid in hungPids) {
            firstSeenUtc.TryAdd(pid, utcNow);
        }

        return hungPids
            .Select(pid => new HungApp(pid, nameOf(pid), (utcNow - firstSeenUtc[pid]).TotalSeconds))
            .ToList();
    }
}
