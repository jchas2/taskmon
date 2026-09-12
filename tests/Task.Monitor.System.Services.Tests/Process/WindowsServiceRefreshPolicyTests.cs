using Task.Monitor.System.Services.Process;

namespace Task.Monitor.System.Services.Tests.Process;

public sealed class WindowsServiceRefreshPolicyTests
{
    [Fact]
    public void ShouldRefresh_Holds_Off_Until_The_Interval_Elapses()
    {
        int cycles = 0;

        // The map is rebuilt on the tenth cycle, not before it. Rebuilding every cycle would mean
        // enumerating the whole service control manager at the sampling rate.
        for (int i = 1; i < WindowsServiceRefreshPolicy.RefreshCycles; i++) {
            Assert.False(WindowsServiceRefreshPolicy.ShouldRefresh(ref cycles, refreshRequested: false));
        }

        Assert.True(WindowsServiceRefreshPolicy.ShouldRefresh(ref cycles, refreshRequested: false));
    }

    [Fact]
    public void ShouldRefresh_Restarts_The_Interval_After_Firing()
    {
        int cycles = 0;

        for (int i = 0; i < WindowsServiceRefreshPolicy.RefreshCycles; i++) {
            WindowsServiceRefreshPolicy.ShouldRefresh(ref cycles, refreshRequested: false);
        }

        Assert.Equal(0, cycles);
        Assert.False(WindowsServiceRefreshPolicy.ShouldRefresh(ref cycles, refreshRequested: false));
    }

    [Fact]
    public void ShouldRefresh_Short_Circuits_On_An_Explicit_Request()
    {
        int cycles = 3;

        Assert.True(WindowsServiceRefreshPolicy.ShouldRefresh(ref cycles, refreshRequested: true));

        // The interval restarts from the request, so a restarting service cannot force a rebuild
        // on every cycle that follows it.
        Assert.Equal(0, cycles);
        Assert.False(WindowsServiceRefreshPolicy.ShouldRefresh(ref cycles, refreshRequested: false));
    }

    [Fact]
    public void HasStalePid_Detects_A_Mapped_Service_That_Is_No_Longer_Running()
    {
        HashSet<int> live = [100, 200, 300];

        Assert.False(WindowsServiceRefreshPolicy.HasStalePid([100, 300], live));

        // 400 was hosting a service last time the map was built and is gone, so the map no longer
        // describes the machine.
        Assert.True(WindowsServiceRefreshPolicy.HasStalePid([100, 400], live));
    }

    [Fact]
    public void HasStalePid_Is_False_For_An_Empty_Map()
    {
        Assert.False(WindowsServiceRefreshPolicy.HasStalePid([], new HashSet<int> { 1, 2 }));
    }
}
