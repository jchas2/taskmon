using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Thermal;
using Xunit.Abstractions;

namespace Task.Monitor.System.Services.Tests.Thermal;

public sealed class ThermalServiceTests(ITestOutputHelper output)
{
    private const int MinimumDelay = 500;

    [Fact]
    public void Should_Run_ThermalService()
    {
        ThermalService service = new();
        service.Start();
        Thread.Sleep((int)(service.Delay * 1.5));
        service.Stop();
    }

    [Fact]
    public void Should_Publish_Well_Formed_Sensors_Through_The_Controller()
    {
        ServiceController controller = new() { Delay = MinimumDelay };
        SystemSnapshot? latest = null;

        controller.SystemSnapshotUpdated += (_, e) => latest = e.Snapshot;
        controller.AddService(() => new CpuService());
        controller.AddService(() => new GpuService());
        controller.AddService(() => new DiskService());
        controller.AddService(() => new ThermalService { Delay = MinimumDelay });
        controller.Start();

        Thread.Sleep(MinimumDelay * 6);
        controller.Stop();

        Assert.NotNull(latest);
        Assert.NotNull(latest!.Thermal);

        foreach (ThermalSensor sensor in latest.Thermal!.Metrics.Sensors) {
            output.WriteLine(
                $"{sensor.Component}/{sensor.ComponentId} '{sensor.SensorName}' " +
                $"{sensor.Celsius:0.0}C via {sensor.Source}" +
                (sensor.CriticalCelsius is { } crit ? $" (crit {crit:0}C)" : ""));

            Assert.False(string.IsNullOrWhiteSpace(sensor.SensorName));
            Assert.True(sensor.Celsius is > -40 and < 150, $"implausible {sensor.Celsius}C");
            Assert.True(Enum.IsDefined(sensor.Component));
            Assert.True(Enum.IsDefined(sensor.Source));
        }

        Assert.Contains(latest.Services, health => health.Name == "Thermal");
        output.WriteLine($"total sensors: {latest.Thermal.Metrics.Sensors.Count}");
    }
}
