namespace Task.Monitor.System.Services.Process;

// When the pid to service map should be rebuilt. Kept separate from the rebuild itself so the
// cadence can be exercised without a service control manager underneath it.
internal static class WindowsServiceRefreshPolicy
{
    // Ten cycles, matching the specs cadence NetworkService uses. At the default 1500ms Delay that
    // is a rebuild roughly every 15 seconds.
    public const int RefreshCycles = 10;

    public static bool ShouldRefresh(ref int cyclesSinceRefresh, bool refreshRequested)
    {
        // An explicit request short circuits the interval and restarts it, so a service that
        // restarts is picked up on the next cycle rather than up to ten cycles later.
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

    // A mapped pid that is no longer running means its service has stopped, or has restarted into
    // a new process. Either way the map no longer describes the machine. This is the analogue of
    // the unknown adapter that triggers a specs refresh in NetworkService, and it costs nothing:
    // the live pid set is already in hand at the end of a cycle.
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
