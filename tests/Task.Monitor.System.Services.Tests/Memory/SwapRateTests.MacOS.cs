#if __APPLE__
using Task.Monitor.System.Services.Memory;

namespace Task.Monitor.System.Services.Tests.Memory;

public sealed class SwapRateTests
{
    private const int MinimumDelay = 500;

    [Fact]
    public void Converts_Swapped_Pages_To_Bytes_Per_Second()
    {
        Assert.Equal(16384 * 10 / 2.0, MemoryService.PagesPerSecondInBytes(100, 110, 2.0, 16384));
    }

    [Fact]
    public void MemoryService_Publishes_Swap_Rates_From_The_Second_Cycle()
    {
        ServiceController controller = new() { Delay = MinimumDelay };
        using SnapshotRecorder snapshots = new(controller);

        controller.AddService(() => new MemoryService { Delay = MinimumDelay });
        controller.Start();

        SystemSnapshot? latest = snapshots.WaitForUpdates(snapshot => snapshot.Memory, count: 3);
        controller.Stop();

        Assert.True(latest!.Memory!.Metrics.SwapInBytesPerSecond >= 0);
        Assert.True(latest.Memory.Metrics.SwapOutBytesPerSecond >= 0);
    }

    [Theory]
    [InlineData(110UL, 100UL, 1.0)]
    [InlineData(100UL, 110UL, 0.0)]
    public void Returns_Null_When_The_Counter_Reset_Or_No_Time_Passed(ulong previous, ulong current, double seconds)
    {
        Assert.Null(MemoryService.PagesPerSecondInBytes(previous, current, seconds, 16384));
    }
}
#endif
