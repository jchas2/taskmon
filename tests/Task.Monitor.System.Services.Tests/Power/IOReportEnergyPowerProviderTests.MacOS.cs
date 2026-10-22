#if __APPLE__
using Task.Monitor.System.Services.Power;
using Task.Monitor.System.Services.Power.Providers.MacOS;

namespace Task.Monitor.System.Services.Tests.Power;

public sealed class IOReportEnergyPowerProviderTests
{
    private static IOReportEnergySampler.Delta Delta(string channel, long value, string unit = "mJ") =>
        new(channel, unit, value);

    [Theory]
    [InlineData("GPU")]
    [InlineData("PCPU0")]
    [InlineData("PCPUDTL42")]
    [InlineData("AMCC")]
    public void MapChannel_Ignores_Per_Core_And_Fabric_Channels(string channel)
    {
        Assert.Null(IOReportEnergyPowerProvider.MapChannel(channel));
    }

    [Theory]
    [InlineData("CPU Energy", PowerComponent.Cpu,          PowerRails.Cpu)]
    [InlineData("ECPU",       PowerComponent.Cpu,          PowerRails.EfficiencyCores)]
    [InlineData("PCPU",       PowerComponent.Cpu,          PowerRails.PerformanceCores)]
    [InlineData("EACC_CPU",   PowerComponent.Cpu,          PowerRails.EfficiencyCores)]
    [InlineData("PACC1_CPU",  PowerComponent.Cpu,          PowerRails.PerformanceCores)]
    [InlineData("GPU Energy", PowerComponent.Gpu,          PowerRails.Gpu)]
    [InlineData("ANE",        PowerComponent.NeuralEngine, PowerRails.NeuralEngine)]
    [InlineData("DRAM",       PowerComponent.Memory,       PowerRails.Dram)]
    [InlineData("DISPEXT",    PowerComponent.Display,      PowerRails.Display)]
    public void MapChannel_Maps_Known_Channels(string channel, PowerComponent component, string rail)
    {
        Assert.Equal((component, rail), IOReportEnergyPowerProvider.MapChannel(channel));
    }

    [Fact]
    public void Read_Reports_Cpu_And_Gpu_Power_From_The_Second_Sample()
    {
        using IOReportEnergySampler sampler = new();
        IOReportEnergyPowerProvider provider = new(sampler, () => []);

        Assert.True(provider.TryInitialise());
        Assert.Empty(provider.Read());

        Thread.Sleep(500);

        List<PowerReading> readings = provider.Read().ToList();

        Assert.Contains(readings, reading => reading.Component == PowerComponent.Cpu && reading.Rail == PowerRails.Cpu);
        Assert.Contains(readings, reading => reading.Component == PowerComponent.Gpu && reading.ComponentId == "0");
        Assert.All(readings, reading => Assert.InRange(reading.Watts, 0, 500));
    }

    [Theory]
    [InlineData(2,             "J",  2.0)]
    [InlineData(1_500,         "mJ", 1.5)]
    [InlineData(2_500_000,     "uJ", 2.5)]
    [InlineData(3_000_000_000, "nJ", 3.0)]
    public void ToJoules_Converts_Each_Unit(long value, string unit, double expected)
    {
        Assert.Equal(expected, IOReportEnergyPowerProvider.ToJoules(value, unit)!.Value, precision: 9);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("W")]
    public void ToJoules_Rejects_An_Unknown_Unit(string? unit)
    {
        Assert.Null(IOReportEnergyPowerProvider.ToJoules(1, unit));
    }

    [Fact]
    public void ToReadings_Derives_The_Cpu_Total_From_Clusters_When_It_Is_Missing()
    {
        List<PowerReading> readings = IOReportEnergyPowerProvider.ToReadings(
            [Delta("EACC_CPU", 200), Delta("PACC0_CPU", 300)], seconds: 1.0, gpuId: "0");

        Assert.Equal(0.5, readings.Single(reading => reading.Rail == PowerRails.Cpu).Watts, precision: 9);
    }

    [Fact]
    public void ToReadings_Divides_Energy_By_The_Interval_In_A_Fixed_Order()
    {
        List<PowerReading> readings = IOReportEnergyPowerProvider.ToReadings(
            [
                Delta("DISP",       100),
                Delta("GPU Energy", 4_000_000_000, "nJ"),
                Delta("CPU Energy", 3_000),
                Delta("PCPU",       2_000),
                Delta("ECPU",       1_000),
                Delta("AMCC",       9_999),
            ],
            seconds: 2.0,
            gpuId: "7");

        Assert.Equal(
            [PowerRails.Cpu, PowerRails.EfficiencyCores, PowerRails.PerformanceCores, PowerRails.Gpu, PowerRails.Display],
            readings.Select(reading => reading.Rail));

        Assert.Equal(1.5,  readings[0].Watts, precision: 9);
        Assert.Equal(0.5,  readings[1].Watts, precision: 9);
        Assert.Equal(1.0,  readings[2].Watts, precision: 9);
        Assert.Equal(2.0,  readings[3].Watts, precision: 9);
        Assert.Equal(0.05, readings[4].Watts, precision: 9);

        Assert.Equal("7", readings[3].ComponentId);
        Assert.All(readings.Where(reading => reading.Component != PowerComponent.Gpu), reading => Assert.Empty(reading.ComponentId));
        Assert.All(readings, reading => Assert.Equal(PowerSource.IOReport, reading.Source));
    }

    [Fact]
    public void ToReadings_Is_Empty_Without_An_Interval()
    {
        Assert.Empty(IOReportEnergyPowerProvider.ToReadings([Delta("CPU Energy", 1_000)], seconds: 0, gpuId: "0"));
    }

    [Fact]
    public void ToReadings_Sums_Channels_That_Share_A_Rail()
    {
        List<PowerReading> readings = IOReportEnergyPowerProvider.ToReadings(
            [Delta("DISP", 100), Delta("DISPEXT", 50), Delta("PACC0_CPU", 200), Delta("PACC1_CPU", 300)],
            seconds: 1.0,
            gpuId: "0");

        Assert.Equal(0.15, readings.Single(reading => reading.Rail == PowerRails.Display).Watts, precision: 9);
        Assert.Equal(0.5,  readings.Single(reading => reading.Rail == PowerRails.PerformanceCores).Watts, precision: 9);
    }
}
#endif
