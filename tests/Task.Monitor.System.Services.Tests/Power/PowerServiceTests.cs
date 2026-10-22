using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Power;
using Xunit.Abstractions;

namespace Task.Monitor.System.Services.Tests.Power;

public sealed class PowerServiceTests(ITestOutputHelper output)
{
    private const int MinimumDelay = 500;

    [Fact]
    public void Should_Publish_Well_Formed_Readings_Through_The_Controller()
    {
        ServiceController controller = new() { Delay = MinimumDelay };
        using SnapshotRecorder snapshots = new(controller);

        controller.AddService(() => new GpuService());
        controller.AddService(() => new DiskService());
        controller.AddService(() => new PowerService { Delay = MinimumDelay });
        controller.Start();

        // Several power cycles, so the providers that read the GPU and disk results have them.
        SystemSnapshot? latest = snapshots.WaitForUpdates(snapshot => snapshot.Power, count: 5);
        controller.Stop();

        Assert.NotNull(latest);
        Assert.NotNull(latest!.Power);

        foreach (PowerReading reading in latest.Power!.Metrics.Readings) {
            output.WriteLine(
                $"{reading.Component}/{reading.ComponentId} '{reading.Rail}' " +
                $"{reading.Watts:0.0} W{(reading.IsRated ? " (rated)" : "")} via {reading.Source}");

            Assert.False(string.IsNullOrWhiteSpace(reading.Rail));
            Assert.True(reading.Watts is >= 0 and < 2000, $"implausible {reading.Watts} W");
            Assert.True(Enum.IsDefined(reading.Component));
            Assert.True(Enum.IsDefined(reading.Source));
        }

        Assert.Contains(latest.Services, health => health.Name == "Power");
        output.WriteLine($"total readings: {latest.Power.Metrics.Readings.Count}");
    }

    [Fact]
    public void Should_Run_PowerService()
    {
        PowerService service = new();
        service.Start();
        Thread.Sleep((int)(service.Delay * 1.5));
        service.Stop();
    }
}
