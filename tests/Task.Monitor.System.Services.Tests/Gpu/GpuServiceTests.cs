using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Tests.Gpu;

public sealed class GpuServiceTests
{
    private const int MinimumDelay = 500;

    [Fact]
    public void Should_Publish_Memory_For_The_Adapters_In_The_Specs()
    {
        ServiceController controller = new() { Delay = MinimumDelay };
        using SnapshotRecorder snapshots = new(controller);

        controller.AddService(new GpuService { Delay = MinimumDelay });
        controller.Start();

        SystemSnapshot? latest = snapshots.WaitForUpdates(snapshot => snapshot.Gpu, count: 2);
        controller.Stop();

        Assert.NotNull(latest?.Gpu);

        GpuInfo gpu = latest!.Gpu!;

        Assert.All(gpu.Metrics.Devices, device => {
            GpuDevice spec = Assert.Single(gpu.Specs.Devices, candidate => candidate.AdapterLuid == device.AdapterLuid);

            Assert.Equal(spec.Index, device.Index);
            Assert.InRange(device.AvailableCombinedGpuMemory, long.MinValue, device.TotalCombinedGpuMemory);
            Assert.InRange(device.GpuPercentTime, 0.0, 1.0);
        });
    }
}
