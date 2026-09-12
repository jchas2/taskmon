using Task.Monitor.Extensions;
using Task.Monitor.System.Services.Power;

namespace Task.Monitor.Tests.Extensions;

public sealed class PowerMetricsExtensionsTests
{
    private static PowerReading Reading(
        PowerComponent component, string id, double watts, bool rated = false) =>
        new() {
            Component = component,
            ComponentId = id,
            Rail = "test",
            Watts = watts,
            IsRated = rated,
            Source = PowerSource.Nvml
        };

    [Fact]
    public void GpuPower_Is_Scoped_To_The_Adapter()
    {
        PowerMetrics metrics = new() {
            Readings = [
                Reading(PowerComponent.Gpu, "111", 120),
                Reading(PowerComponent.Gpu, "222", 15),
            ]
        };

        Assert.Equal(120, metrics.GpuPower(111));
        Assert.Equal(15, metrics.GpuPower(222));
        Assert.Null(metrics.GpuPower(999));
    }

    [Fact]
    public void DiskPower_Returns_The_Reading_With_Its_Rated_Flag()
    {
        PowerMetrics metrics = new() {
            Readings = [Reading(PowerComponent.Disk, "0", 5.5, rated: true)]
        };

        PowerReading? reading = metrics.DiskPower(0);

        Assert.NotNull(reading);
        Assert.Equal(5.5, reading!.Watts);
        Assert.True(reading.IsRated);
        Assert.Null(metrics.DiskPower(1));
    }

    [Fact]
    public void SystemPower_Ignores_Component_Id()
    {
        PowerMetrics metrics = new() {
            Readings = [Reading(PowerComponent.System, "", 42)]
        };

        Assert.Equal(42, metrics.SystemPower());
    }

    [Fact]
    public void Formats_Watts()
    {
        Assert.Equal("31.0 W", 31.04.ToWattText());
        Assert.Equal("N/A", ((double?)null).ToWattText());
    }
}
