using Task.Monitor.System.Services.Health;
using Task.Monitor.System.Services.Thermal;

namespace Task.Monitor.System.Services.Tests.Health;

public sealed class HealthThresholdsTests
{
    private static readonly HealthThresholds Defaults = new();

    [Fact]
    public void A_Falling_Pair_The_Wrong_Way_Round_Falls_Back_As_A_Pair()
    {
        HealthThresholds thresholds = new HealthThresholds {
            VolumeFreeWarningPercent  = 2,
            VolumeFreeCriticalPercent = 20
        }.Validated();

        Assert.Equal(Defaults.VolumeFreeWarningPercent, thresholds.VolumeFreeWarningPercent);
        Assert.Equal(Defaults.VolumeFreeCriticalPercent, thresholds.VolumeFreeCriticalPercent);
    }

    [Fact]
    public void A_Negative_Duration_Falls_Back_To_Its_Default()
    {
        HealthThresholds thresholds = new HealthThresholds { WarningSustainSeconds = -1, SustainGraceSeconds = -1 }.Validated();

        Assert.Equal(Defaults.WarningSustainSeconds, thresholds.WarningSustainSeconds);
        Assert.Equal(Defaults.SustainGraceSeconds, thresholds.SustainGraceSeconds);
    }

    [Fact]
    public void A_Percentage_Above_100_Falls_Back_To_Its_Default()
    {
        HealthThresholds thresholds = new HealthThresholds { DiskActiveWarningPercent = 150, HardFaultsAvailablePercent = 150 }.Validated();

        Assert.Equal(Defaults.DiskActiveWarningPercent, thresholds.DiskActiveWarningPercent);
        Assert.Equal(Defaults.HardFaultsAvailablePercent, thresholds.HardFaultsAvailablePercent);
    }

    [Fact]
    public void A_Rising_Pair_The_Wrong_Way_Round_Falls_Back_As_A_Pair()
    {
        HealthThresholds thresholds = new HealthThresholds { CpuWarningPercent = 99, CpuCriticalPercent = 50 }.Validated();

        Assert.Equal(Defaults.CpuWarningPercent, thresholds.CpuWarningPercent);
        Assert.Equal(Defaults.CpuCriticalPercent, thresholds.CpuCriticalPercent);
    }

    [Fact]
    public void Disk_Latency_May_Exceed_100_But_Must_Stay_In_Order()
    {
        Assert.Equal(500, new HealthThresholds { DiskLatencyCriticalMs = 500 }.Validated().DiskLatencyCriticalMs);

        HealthThresholds reversed = new HealthThresholds { DiskLatencyWarningMs = 300, DiskLatencyCriticalMs = 200 }.Validated();

        Assert.Equal(Defaults.DiskLatencyWarningMs, reversed.DiskLatencyWarningMs);
        Assert.Equal(Defaults.DiskLatencyCriticalMs, reversed.DiskLatencyCriticalMs);
    }

    [Fact]
    public void Not_Responding_Seconds_May_Exceed_100_But_Must_Stay_In_Order()
    {
        Assert.Equal(300, new HealthThresholds { NotRespondingCriticalSeconds = 300 }.Validated().NotRespondingCriticalSeconds);

        HealthThresholds reversed = new HealthThresholds { NotRespondingWarningSeconds = 60, NotRespondingCriticalSeconds = 10 }.Validated();

        Assert.Equal(Defaults.NotRespondingWarningSeconds, reversed.NotRespondingWarningSeconds);
        Assert.Equal(Defaults.NotRespondingCriticalSeconds, reversed.NotRespondingCriticalSeconds);
    }

    [Fact]
    public void Thermal_Pressure_Levels_Must_Stay_In_Order()
    {
        HealthThresholds thresholds = new HealthThresholds {
            ThermalPressureWarning  = ThermalPressure.Trapping,
            ThermalPressureCritical = ThermalPressure.Moderate
        }.Validated();

        Assert.Equal(Defaults.ThermalPressureWarning, thresholds.ThermalPressureWarning);
        Assert.Equal(Defaults.ThermalPressureCritical, thresholds.ThermalPressureCritical);
    }

    [Fact]
    public void Valid_Values_Are_Kept()
    {
        HealthThresholds custom = new() {
            CpuWarningPercent         = 70,
            CpuCriticalPercent        = 85,
            VolumeFreeWarningPercent  = 20,
            VolumeFreeCriticalPercent = 15,
            WarningSustainSeconds     = 0,
            EventHistorySize          = 5
        };

        Assert.Equal(custom, custom.Validated());
    }
}
