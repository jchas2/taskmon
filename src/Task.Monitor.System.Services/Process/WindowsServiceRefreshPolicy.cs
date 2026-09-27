namespace Task.Monitor.System.Services.Process;

internal static class WindowsServiceRefreshPolicy
{
    public const int RefreshCycles = 10;

    public static bool ShouldRefresh(ref int cyclesSinceRefresh, bool refreshRequested)
    {
        if (refreshRequested) {
            cyclesSinceRefresh = 0;
            return true;
        }

        if (++cyclesSinceRefresh < RefreshCycles) {
            return false;
        }

        cyclesSinceRefresh = 0;
        return true;
    }

    public static bool HasStalePid(IEnumerable<int> mappedPids, ICollection<int> livePids)
    {
        foreach (int pid in mappedPids) {
            if (!livePids.Contains(pid)) {
                return true;
            }
        }

        return false;
    }
}
