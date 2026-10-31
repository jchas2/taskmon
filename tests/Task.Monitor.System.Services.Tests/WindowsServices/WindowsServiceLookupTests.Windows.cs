#if __WIN32__
using Task.Monitor.System.Services.WindowsServices;

namespace Task.Monitor.System.Services.Tests.WindowsServices;

[Collection(WindowsServiceLookupCollection.Name)]
public sealed partial class WindowsServiceLookupTests
{
    [Fact]
    public void GetService_Maps_Each_Active_Process_To_Its_Last_Enumerated_Service()
    {
        SyncToIntervalStart();

        Dictionary<int, WindowsServiceInfo> expected = WindowsServiceLookup.BuildServiceMap(WindowsServiceEnumerator.GetServiceProcesses());

        // Services can start and stop between the two enumerations, so compare only the processes
        // both saw.
        int compared = 0;

        foreach ((int pid, WindowsServiceInfo service) in expected) {
            if (WindowsServiceLookup.GetService(pid, out WindowsServiceInfo? mapped)) {
                Assert.Equal(service.ServiceName, mapped!.ServiceName);
                compared++;
            }
        }

        Assert.True(compared > 0);
        Assert.False(WindowsServiceLookup.GetService(pid: 0, out _));
    }

    [Fact]
    public void RefreshIfDue_Rebuilds_Only_Once_The_Interval_Elapses()
    {
        SyncToIntervalStart();

        int before = WindowsServiceLookup.RebuildCount;

        for (int i = 0; i < WindowsServiceRefreshPolicy.RefreshCycles - 1; i++) {
            WindowsServiceLookup.RefreshIfDue();
        }

        // Rebuilding every cycle would mean enumerating the whole service control manager at the
        // sampling rate, which is why the original built the map once and never again.
        Assert.Equal(before, WindowsServiceLookup.RebuildCount);

        WindowsServiceLookup.RefreshIfDue();

        Assert.Equal(before + 1, WindowsServiceLookup.RebuildCount);
    }

    [Fact]
    public void RequestRefreshIfStale_Brings_The_Rebuild_Forward()
    {
        SyncToIntervalStart();

        if (WindowsServiceLookup.MappedServiceCount == 0) {
            // No service handle on this machine could be opened, so there is nothing to go stale.
            return;
        }

        int before = WindowsServiceLookup.RebuildCount;

        // An empty live set makes every mapped service look as though its process has gone.
        WindowsServiceLookup.RequestRefreshIfStale(new HashSet<int>());
        WindowsServiceLookup.RefreshIfDue();

        Assert.Equal(before + 1, WindowsServiceLookup.RebuildCount);
    }

    // The refresh counters are static and other tests in this collection will have advanced them,
    // so drive cycles until a rebuild fires. The interval is then known to have just restarted.
    private static void SyncToIntervalStart()
    {
        WindowsServiceLookup.GetService(pid: 0, out _);

        int baseline = WindowsServiceLookup.RebuildCount;

        for (int i = 0;
             i <= WindowsServiceRefreshPolicy.RefreshCycles && WindowsServiceLookup.RebuildCount == baseline;
             i++) {

            WindowsServiceLookup.RefreshIfDue();
        }
    }

    [Fact]
    public void The_Map_Survives_Repeated_Rebuilds()
    {
        WindowsServiceLookup.GetService(pid: 0, out _);

        int mapped = WindowsServiceLookup.MappedServiceCount;

        for (int cycle = 0; cycle < WindowsServiceRefreshPolicy.RefreshCycles * 3; cycle++) {
            WindowsServiceLookup.RefreshIfDue();
        }

        // A rebuild that cleared first and repopulated would blank every daemon flag whenever the
        // service control manager was momentarily unavailable.
        Assert.Equal(mapped, WindowsServiceLookup.MappedServiceCount);
    }
}
#endif
