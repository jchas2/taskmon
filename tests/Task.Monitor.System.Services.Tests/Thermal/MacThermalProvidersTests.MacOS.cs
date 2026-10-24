#if __APPLE__
using Task.Monitor.Interop.Mach;
using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Thermal;
using Task.Monitor.System.Services.Thermal.Providers.MacOS;
using Xunit.Abstractions;

namespace Task.Monitor.System.Services.Tests.Thermal;

public sealed class MacThermalProvidersTests(ITestOutputHelper output)
{
    private const int MinimumDelay = 500;

    [Theory]
    [InlineData("Tf21", true)]
    [InlineData("Ts0P", true)]
    [InlineData("TH0x", true)]
    [InlineData("PSTR", true)]
    [InlineData("Tp0P", false)]
    [InlineData("TPCD", false)]
    public void Classify_Drops_Unknown_Keys(string key, bool appleSilicon)
    {
        Assert.Null(SmcThermalKeys.Classify(key, appleSilicon));
    }

    [Theory]
    [InlineData("Tp0K", true,  ThermalComponent.Cpu,     SmcThermalKeys.PerformanceCores)]
    [InlineData("Te05", true,  ThermalComponent.Cpu,     SmcThermalKeys.EfficiencyCores)]
    [InlineData("Tg0D", true,  ThermalComponent.Gpu,     "")]
    [InlineData("TaLP", true,  ThermalComponent.Ambient, "")]
    [InlineData("TB0T", true,  ThermalComponent.Battery, "")]
    [InlineData("TB2T", false, ThermalComponent.Battery, "")]
    [InlineData("TC0D", false, ThermalComponent.Cpu,     "")]
    [InlineData("TC3C", false, ThermalComponent.Cpu,     "")]
    [InlineData("TCXC", false, ThermalComponent.Cpu,     "")]
    [InlineData("TG0P", false, ThermalComponent.Gpu,     "")]
    [InlineData("TA0P", false, ThermalComponent.Ambient, "")]
    public void Classify_Groups_Known_Temperature_Keys(
        string key, bool appleSilicon, ThermalComponent component, string cluster)
    {
        Assert.Equal(new SmcThermalKeys.Group(component, cluster), SmcThermalKeys.Classify(key, appleSilicon));
    }

    [Fact]
    public void Hottest_Drops_A_Group_With_No_Plausible_Reading()
    {
        SmcThermalKeys.Group gpu = new(ThermalComponent.Gpu, string.Empty);

        Assert.Empty(SmcThermalKeys.Hottest([(gpu, 0.0), (gpu, -12.0)]));
    }

    [Fact]
    public void Hottest_Reports_The_Hottest_Plausible_Reading_Per_Group()
    {
        SmcThermalKeys.Group pCores = new(ThermalComponent.Cpu, SmcThermalKeys.PerformanceCores);
        SmcThermalKeys.Group eCores = new(ThermalComponent.Cpu, SmcThermalKeys.EfficiencyCores);

        Dictionary<SmcThermalKeys.Group, double> hottest = SmcThermalKeys.Hottest([
            (pCores, 41.4), (pCores, 67.1), (pCores, 52.0), (pCores, 140.0),
            (eCores, 49.1), (eCores, 3.0),
        ]).ToDictionary(reading => reading.Group, reading => reading.Celsius);

        Assert.Equal(67.1, hottest[pCores]);
        Assert.Equal(49.1, hottest[eCores]);
    }

    [Fact]
    public void InternalDiskId_Picks_The_Fixed_Apple_Fabric_Disk()
    {
        DiskDevice[] disks = [
            new() { Index = 4, BusType = "USB" },
            new() { Index = 6, BusType = "Apple Fabric", IsRemovable = true },
            new() { Index = 0, BusType = "Apple Fabric" },
        ];

        Assert.Equal("0", HidThermalProvider.InternalDiskId(disks));
        Assert.Null(HidThermalProvider.InternalDiskId([new DiskDevice { Index = 2, BusType = "USB" }]));
    }

    [Theory]
    [InlineData("NAND CH0 temp", true)]
    [InlineData("NAND CH1 temp", true)]
    [InlineData("PMU tdie1",     false)]
    [InlineData(null,            false)]
    public void IsNandSensor_Matches_The_Nand_Channels(string? product, bool expected)
    {
        Assert.Equal(expected, HidThermalProvider.IsNandSensor(product));
    }

    [SkippableFact]
    public void ThermalService_Publishes_Cpu_Temperatures_Fans_And_Pressure()
    {
        using (Smc? smc = Smc.Open()) {
            Skip.If(smc is null, "AppleSMC is not available");
        }

        ServiceController controller = new() { Delay = MinimumDelay };
        using SnapshotRecorder snapshots = new(controller);

        controller.AddService(() => new CpuService());
        controller.AddService(() => new GpuService());
        controller.AddService(() => new DiskService());
        controller.AddService(() => new ThermalService { Delay = MinimumDelay });
        controller.Start();

        SystemSnapshot? latest = snapshots.WaitForUpdates(snapshot => snapshot.Thermal, count: 3);
        controller.Stop();

        ThermalMetrics metrics = latest!.Thermal!.Metrics;

        foreach (ThermalSensor sensor in metrics.Sensors) {
            output.WriteLine($"{sensor.Component}/{sensor.ComponentId} {sensor.SensorName} {sensor.Celsius:0.0}C via {sensor.Source}");
        }

        foreach (FanReading fan in metrics.Fans) {
            output.WriteLine($"{fan.Name} {fan.Rpm:0} rpm ({fan.MinRpm:0}-{fan.MaxRpm:0})");
        }

        output.WriteLine($"pressure: {metrics.Pressure}");

        Assert.Contains(metrics.Sensors, sensor =>
            sensor is { Component: ThermalComponent.Cpu, Source: ThermalSource.Smc, Celsius: > 10 and < 110 });
        Assert.NotNull(metrics.Pressure);
        Assert.All(metrics.Fans, fan => Assert.True(fan.MaxRpm >= fan.MinRpm));
    }

    [Theory]
    [InlineData(0UL, ThermalPressure.Nominal)]
    [InlineData(1UL, ThermalPressure.Moderate)]
    [InlineData(2UL, ThermalPressure.Heavy)]
    [InlineData(3UL, ThermalPressure.Trapping)]
    [InlineData(4UL, ThermalPressure.Sleeping)]
    public void ToPressure_Maps_The_Notify_Levels(ulong level, ThermalPressure expected)
    {
        Assert.Equal(expected, ThermalPressureMonitor.ToPressure(level));
    }

    [Fact]
    public void ToPressure_Returns_Null_For_An_Unknown_Level()
    {
        Assert.Null(ThermalPressureMonitor.ToPressure(10));
    }
}
#endif
