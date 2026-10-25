#if __APPLE__
using Task.Monitor.System.Services.Disk;
using Xunit.Abstractions;

namespace Task.Monitor.System.Services.Tests.Disk;

public sealed class VirtualDiskTests(ITestOutputHelper output)
{
    private const int MinimumDelay = 500;

    [Fact]
    public void DiskService_Leaves_Out_Disk_Images()
    {
        ServiceController controller = new() { Delay = MinimumDelay };
        using SnapshotRecorder snapshots = new(controller);

        controller.AddService(() => new DiskService { Delay = MinimumDelay });
        controller.Start();

        SystemSnapshot? latest = snapshots.WaitForUpdates(snapshot => snapshot.Disk, count: 3);
        controller.Stop();

        DiskInfo disk = latest!.Disk!;

        foreach (DiskDevice device in disk.Specs.Devices) {
            output.WriteLine($"disk{device.Index} {device.Model} via {device.BusType}");
        }

        Assert.DoesNotContain(disk.Specs.Devices, device => device.BusType == "Virtual Interface");
        Assert.Contains(disk.Specs.Devices, device => device.Index == 0);
        Assert.All(disk.Metrics.Devices, metric => Assert.Contains(disk.Specs.Devices, device => device.Index == metric.Index));
    }

    [Theory]
    [InlineData("Virtual Interface", true)]
    [InlineData("Apple Fabric",      false)]
    [InlineData("PCI-Express",       false)]
    [InlineData("USB",               false)]
    [InlineData("Thunderbolt",       false)]
    [InlineData("Secure Digital",    false)]
    [InlineData(null,                false)]
    public void IsVirtualInterconnect_Only_Matches_Disk_Images(string? interconnect, bool expected)
    {
        Assert.Equal(expected, DiskService.IsVirtualInterconnect(interconnect));
    }
}
#endif
