using Task.Monitor.System.Services.Disk;

namespace Task.Monitor.System.Services.Tests.Disk;

public sealed class DiskServiceTests
{
    private const int MinimumDelay = 500;

    [Fact]
    public void Should_Publish_Each_Disk_In_Index_Order_With_The_Busiest_As_The_Active_Time()
    {
        ServiceController controller = new() { Delay = MinimumDelay };
        using SnapshotRecorder snapshots = new(controller);

        controller.AddService(new DiskService { Delay = MinimumDelay });
        controller.Start();

        // The first cycle primes the counters, so rates arrive from the third.
        SystemSnapshot? latest = snapshots.WaitForUpdates(snapshot => snapshot.Disk, count: 3);
        controller.Stop();

        Assert.NotNull(latest?.Disk);

        DiskMetrics metrics = latest!.Disk!.Metrics;

        Assert.NotEmpty(metrics.Devices);
        Assert.Equal(metrics.Devices.OrderBy(device => device.Index).Select(device => device.Index), metrics.Devices.Select(device => device.Index));
        Assert.All(metrics.Devices, device => Assert.InRange(device.PercentActiveTime, 0.0, 100.0));
        Assert.Equal(metrics.Devices.Max(device => device.PercentActiveTime), metrics.PercentActiveTime);
        Assert.True(metrics.ReadBytesPerSecond >= 0 && metrics.WriteBytesPerSecond >= 0);
    }

    [Fact]
    public void Should_Run_DiskSystemService()
    {
        DiskService service = new();
        service.Start();
        Thread.Sleep((int)(service.Delay * 1.5));
        service.Stop();
    }
}
