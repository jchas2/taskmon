#if __APPLE__
using Task.Monitor.Interop.Mach;
using Task.Monitor.System.Services.Memory;

namespace Task.Monitor.System.Services.Tests.Memory;

public sealed class UsedMemoryPagesTests
{
    [Fact]
    public void Counts_App_Wired_And_Compressed_Pages_But_Not_The_File_Cache()
    {
        MachHost.VmStatistics64 stats = new() {
            active_count          = 685_643,
            inactive_count        = 646_703,
            wire_count            = 289_811,
            purgeable_count       = 56_031,
            compressor_page_count = 443_580,
            external_page_count   = 378_322,
            internal_page_count   = 995_968
        };

        Assert.Equal(995_968UL - 56_031 + 289_811 + 443_580, MachHost.UsedMemoryPages(stats));
    }

    [Fact]
    public void Does_Not_Underflow_When_Purgeable_Exceeds_App_Pages()
    {
        MachHost.VmStatistics64 stats = new() { internal_page_count = 10, purgeable_count = 20, wire_count = 5 };

        Assert.Equal(5UL, MachHost.UsedMemoryPages(stats));
    }

    [Fact]
    public void MemoryService_Publishes_Used_Memory_Below_The_Total()
    {
        ServiceController controller = new() { Delay = WorkerService.MinimumDelayInMilliseconds };
        using SnapshotRecorder snapshots = new(controller);

        controller.AddService(() => new MemoryService());
        controller.Start();

        SystemSnapshot? latest = snapshots.WaitForUpdates(snapshot => snapshot.Memory, count: 2);
        controller.Stop();

        MemoryMetrics metrics = latest!.Memory!.Metrics;

        Assert.True(metrics.TotalPhysical > 0);
        Assert.InRange(metrics.AvailablePhysical, 1UL, metrics.TotalPhysical);
    }
}
#endif
