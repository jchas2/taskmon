using Task.Monitor.Extensions;
using Task.Monitor.System.Services.Thermal;

namespace Task.Monitor.Tests.Extensions;

public sealed class ThermalMetricsExtensionsTests
{
    private static ThermalSensor Sensor(
        ThermalComponent component, string id, string name, double celsius, ThermalSource source) =>
        new() {
            Component = component,
            ComponentId = id,
            SensorName = name,
            Celsius = celsius,
            Source = source
        };

    [Fact]
    public void CpuTemperature_Returns_The_Acpi_Zone_Reading()
    {
        ThermalMetrics metrics = new() {
            Sensors = [Sensor(ThermalComponent.Cpu, "", "ACPI TZ00", 55, ThermalSource.AcpiThermalZone)]
        };

        Assert.Equal(55, metrics.CpuTemperature());
    }

    [Fact]
    public void GpuTemperature_Prefers_The_Vendor_Sdk_Over_An_Acpi_Zone()
    {
        ThermalMetrics metrics = new() {
            Sensors = [
                Sensor(ThermalComponent.Gpu, "42", "GPU", 70, ThermalSource.AcpiThermalZone),
                Sensor(ThermalComponent.Gpu, "42", "Hot Spot", 64, ThermalSource.NvApi),
            ]
        };

        Assert.Equal(64, metrics.GpuTemperature(42));
    }

    [Fact]
    public void GpuTemperature_Prefers_The_Canonical_Sensor_Over_A_Hotter_One()
    {
        ThermalMetrics metrics = new() {
            Sensors = [
                Sensor(ThermalComponent.Gpu, "42", "GPU", 60, ThermalSource.NvApi),
                Sensor(ThermalComponent.Gpu, "42", "Memory", 72, ThermalSource.NvApi),
            ]
        };

        // "GPU" is the headline reading; a hotter memory-junction sensor must not hijack it.
        Assert.Equal(60, metrics.GpuTemperature(42));
    }

    [Fact]
    public void GpuTemperature_Falls_Back_To_The_Hottest_When_No_Sensor_Is_Canonical()
    {
        ThermalMetrics metrics = new() {
            Sensors = [
                Sensor(ThermalComponent.Gpu, "42", "Hot Spot", 71, ThermalSource.NvApi),
                Sensor(ThermalComponent.Gpu, "42", "Board", 55, ThermalSource.NvApi),
            ]
        };

        Assert.Equal(71, metrics.GpuTemperature(42));
    }

    [Fact]
    public void DiskTemperature_Prefers_The_Composite_Over_An_Internal_Sensor()
    {
        ThermalMetrics metrics = new() {
            Sensors = [
                Sensor(ThermalComponent.Disk, "0", "Composite", 35, ThermalSource.NvmeHealthLog),
                Sensor(ThermalComponent.Disk, "0", "Sensor 2", 63, ThermalSource.NvmeHealthLog),
            ]
        };

        Assert.Equal(35, metrics.DiskTemperature(0));
    }

    [Fact]
    public void DiskTemperature_Is_Scoped_To_The_Requested_Index()
    {
        ThermalMetrics metrics = new() {
            Sensors = [
                Sensor(ThermalComponent.Disk, "0", "Composite", 35, ThermalSource.NvmeHealthLog),
                Sensor(ThermalComponent.Disk, "1", "Composite", 48, ThermalSource.NvmeHealthLog),
            ]
        };

        Assert.Equal(35, metrics.DiskTemperature(0));
        Assert.Equal(48, metrics.DiskTemperature(1));
    }

    [Fact]
    public void Returns_Null_When_No_Matching_Sensor_Exists()
    {
        ThermalMetrics metrics = new();

        Assert.Null(metrics.CpuTemperature());
        Assert.Null(metrics.GpuTemperature(1));
        Assert.Null(metrics.DiskTemperature(0));
    }

    [Fact]
    public void Formats_A_Temperature()
    {
        Assert.Equal("62°C", 61.6.ToTemperatureText());
        Assert.Equal("N/A", ((double?)null).ToTemperatureText());
    }
}
