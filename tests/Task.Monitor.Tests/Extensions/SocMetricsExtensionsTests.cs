using Task.Monitor.Extensions;
using Task.Monitor.System.Services.Soc;

namespace Task.Monitor.Tests.Extensions;

public sealed class SocMetricsExtensionsTests
{
    [Fact]
    public void ToFormattedByteRate_Shows_NA_Without_A_Rate()
    {
        Assert.Equal("N/A", ((double?)null).ToFormattedByteRate());
    }

    [Fact]
    public void ToGpuFrequencyText_Shows_Idle_When_The_Gpu_Did_Not_Run()
    {
        Assert.Equal("Idle", new SocMetrics().ToGpuFrequencyText());
        Assert.Equal($"{1380:N0} MHz", new SocMetrics { GpuFrequencyMhz = 1380 }.ToGpuFrequencyText());
    }

    [Theory]
    [InlineData(null,  null,  "None")]
    [InlineData(0.005, 0.0,   "None")]
    [InlineData(0.25,  null,  "Thermal 25%")]
    [InlineData(null,  0.4,   "Power 40%")]
    [InlineData(0.1,   0.05,  "Thermal 10% · Power 5%")]
    public void ToGpuLimitText_Names_Each_Limit_Above_One_Percent(double? thermal, double? power, string expected)
    {
        SocMetrics metrics = new() { GpuThermalLimitRatio = thermal, GpuPowerLimitRatio = power };

        Assert.Equal(expected, metrics.ToGpuLimitText());
    }
}
