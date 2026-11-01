using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Health;
using Task.Monitor.System.Services.Health.Rules;
using Task.Monitor.System.Services.Memory;
using Task.Monitor.System.Services.Network;
using Task.Monitor.System.Services.Soc;
using Task.Monitor.System.Services.Thermal;

namespace Task.Monitor.System.Services.Tests.Health;

public sealed class HealthRulesTests
{
    private const ulong Gigabyte = 1024UL * 1024UL * 1024UL;

    private readonly HealthThresholds thresholds = new();

    private static HealthSample Cpu(CpuMetrics metrics) =>
        new() { Cpu = new CpuInfo { Metrics = metrics } };

    // Regression test: sample timing put a real reading at 100.8%, shown as "101% busy".
    [Fact]
    public void Cpu_Busy_Time_Stops_At_100_Percent()
    {
        HealthReading reading = new CpuHealthRule().Evaluate(Cpu(new CpuMetrics { CpuTotalTime = 1.008 }), thresholds);

        Assert.Equal("100%", reading.Utilisation);
        Assert.Equal("100% busy", reading.Reason);
    }

    [Theory]
    [InlineData(0.50, HealthLevel.Ok)]
    [InlineData(0.90, HealthLevel.Warning)]
    [InlineData(0.98, HealthLevel.Critical)]
    public void Cpu_Is_Judged_On_Busy_Time(double busy, HealthLevel expected)
    {
        HealthReading reading = new CpuHealthRule().Evaluate(Cpu(new CpuMetrics { CpuTotalTime = busy }), thresholds);

        Assert.Equal(expected, reading.Level);
    }

    [Fact]
    public void Cpu_Queue_And_Driver_Time_Raise_A_Warning_On_An_Otherwise_Quiet_Cpu()
    {
        CpuHealthRule rule = new();

        Assert.Equal(HealthLevel.Warning, rule.Evaluate(Cpu(new CpuMetrics { RunQueuePerCore = 2.5 }), thresholds).Level);
        Assert.Equal(HealthLevel.Warning, rule.Evaluate(Cpu(new CpuMetrics { DriverTimePercent = 20.0 }), thresholds).Level);
    }

    private static HealthSample Disk(DiskMetrics metrics) =>
        new() { Disk = new DiskInfo { Metrics = metrics } };

    [Fact]
    public void Disk_Active_Time_Does_Not_Count_When_There_Is_Latency_To_Read()
    {
        HealthSample sample = Disk(new DiskMetrics { PercentActiveTime = 100.0, LatencyMs = 2.0 });

        Assert.Equal(HealthLevel.Ok, new DiskHealthRule().Evaluate(sample, thresholds).Level);
    }

    [Fact]
    public void Disk_Falls_Back_To_Active_Time_With_No_Latency()
    {
        HealthSample sample = Disk(new DiskMetrics { PercentActiveTime = 95.0 });

        Assert.Equal(HealthLevel.Warning, new DiskHealthRule().Evaluate(sample, thresholds).Level);
    }

    [Theory]
    [InlineData(5.0, HealthLevel.Ok)]
    [InlineData(25.0, HealthLevel.Warning)]
    [InlineData(150.0, HealthLevel.Critical)]
    public void Disk_Is_Judged_On_Latency(double latency, HealthLevel expected)
    {
        HealthReading reading = new DiskHealthRule().Evaluate(Disk(new DiskMetrics { LatencyMs = latency }), thresholds);

        Assert.Equal(expected, reading.Level);
    }

    [Fact]
    public void Gpu_Held_Back_By_A_Limit_Is_A_Warning()
    {
        HealthSample sample = new() {
            Gpu = new GpuInfo(),
            Soc = new SocInfo { Metrics = new SocMetrics { GpuThermalLimitRatio = 0.25 } }
        };

        HealthReading reading = new GpuHealthRule().Evaluate(sample, thresholds);

        Assert.Equal(HealthLevel.Warning, reading.Level);
        Assert.Contains("heat", reading.Reason);
    }

    [Fact]
    public void Gpu_With_Low_Memory_Is_A_Warning()
    {
        HealthSample sample = new() {
            Gpu = new GpuInfo { Metrics = new GpuMetrics { TotalCombinedGpuMemory = 100, AvailableCombinedGpuMemory = 5 } }
        };

        Assert.Equal(HealthLevel.Warning, new GpuHealthRule().Evaluate(sample, thresholds).Level);
    }

    [Fact]
    public void Gpu_With_No_Memory_Figures_Is_Ok()
    {
        HealthSample sample = new() { Gpu = new GpuInfo() };

        Assert.Equal(HealthLevel.Ok, new GpuHealthRule().Evaluate(sample, thresholds).Level);
    }

    private static HealthSample Memory(MemoryMetrics metrics) =>
        new() { Memory = new MemoryInfo { Metrics = metrics } };

    // Regression test: Windows paging programs back in after a memory test looked like pressure,
    // with 85% of memory available.
    [Fact]
    public void Memory_Hard_Faults_Do_Not_Count_With_Plenty_Available()
    {
        HealthSample sample = Memory(new MemoryMetrics {
            TotalPhysical       = 100 * Gigabyte,
            AvailablePhysical   = 85 * Gigabyte,
            HardFaultsPerSecond = 36000.0
        });

        Assert.Equal(HealthLevel.Ok, new MemoryHealthRule().Evaluate(sample, thresholds).Level);
    }

    [Fact]
    public void Memory_Hard_Faults_Follow_The_Available_Setting()
    {
        HealthThresholds relaxed = new() { HardFaultsAvailablePercent = 90 };

        HealthSample sample = Memory(new MemoryMetrics {
            TotalPhysical       = 100 * Gigabyte,
            AvailablePhysical   = 85 * Gigabyte,
            HardFaultsPerSecond = 800.0
        });

        Assert.Equal(HealthLevel.Warning, new MemoryHealthRule().Evaluate(sample, relaxed).Level);
    }

    [Fact]
    public void Memory_Hard_Faults_Raise_A_Warning_When_Memory_Is_Short()
    {
        HealthSample sample = Memory(new MemoryMetrics {
            TotalPhysical       = 100 * Gigabyte,
            AvailablePhysical   = 20 * Gigabyte,
            HardFaultsPerSecond = 800.0
        });

        Assert.Equal(HealthLevel.Warning, new MemoryHealthRule().Evaluate(sample, thresholds).Level);
    }

    [Theory]
    [InlineData(50UL, HealthLevel.Ok)]
    [InlineData(9UL, HealthLevel.Warning)]
    [InlineData(4UL, HealthLevel.Critical)]
    public void Memory_Is_Judged_On_Available_Memory(ulong availablePercent, HealthLevel expected)
    {
        HealthSample sample = Memory(new MemoryMetrics {
            TotalPhysical     = 100 * Gigabyte,
            AvailablePhysical = availablePercent * Gigabyte
        });

        Assert.Equal(expected, new MemoryHealthRule().Evaluate(sample, thresholds).Level);
    }

    [Theory]
    [InlineData(80.0, HealthLevel.Ok)]
    [InlineData(92.0, HealthLevel.Warning)]
    [InlineData(98.0, HealthLevel.Critical)]
    public void Memory_Is_Judged_On_Commit(double commitPercent, HealthLevel expected)
    {
        HealthSample sample = Memory(new MemoryMetrics {
            TotalPhysical     = 100 * Gigabyte,
            AvailablePhysical = 50 * Gigabyte,
            CommitLimit       = 100.0,
            CommitTotal       = commitPercent
        });

        Assert.Equal(expected, new MemoryHealthRule().Evaluate(sample, thresholds).Level);
    }

    [Fact]
    public void Memory_Prefers_The_Kernel_Pressure_Level_To_Available_Memory()
    {
        HealthSample sample = Memory(new MemoryMetrics {
            TotalPhysical     = 100 * Gigabyte,
            AvailablePhysical = 1 * Gigabyte,
            Pressure          = MemoryPressure.Normal
        });

        Assert.Equal(HealthLevel.Ok, new MemoryHealthRule().Evaluate(sample, thresholds).Level);
    }

    [Theory]
    [InlineData(MemoryPressure.Warning, HealthLevel.Warning)]
    [InlineData(MemoryPressure.Critical, HealthLevel.Critical)]
    public void Memory_Pressure_Levels_Map_Across(MemoryPressure pressure, HealthLevel expected)
    {
        HealthSample sample = Memory(new MemoryMetrics { TotalPhysical = Gigabyte, Pressure = pressure });

        Assert.Equal(expected, new MemoryHealthRule().Evaluate(sample, thresholds).Level);
    }

    [Fact]
    public void Memory_Swap_Out_Only_Counts_With_A_Kernel_Pressure_Level()
    {
        const double swapOut = 5.0 * 1024 * 1024;

        HealthSample windows = Memory(new MemoryMetrics {
            TotalPhysical = 100 * Gigabyte, AvailablePhysical = 50 * Gigabyte, SwapOutBytesPerSecond = swapOut
        });

        HealthSample macos = Memory(new MemoryMetrics {
            TotalPhysical = 100 * Gigabyte, Pressure = MemoryPressure.Normal, SwapOutBytesPerSecond = swapOut
        });

        Assert.Equal(HealthLevel.Ok, new MemoryHealthRule().Evaluate(windows, thresholds).Level);
        Assert.Equal(HealthLevel.Warning, new MemoryHealthRule().Evaluate(macos, thresholds).Level);
    }

    [Fact]
    public void Network_Errors_Raise_A_Warning()
    {
        NetworkHealthRule rule = new();

        HealthSample clean  = new() { Network = new NetworkInfo() };
        HealthSample errors = new() { Network = new NetworkInfo { Metrics = new NetworkMetrics { ErrorsPerSecond = 1.5 } } };

        Assert.Equal(HealthLevel.Ok, rule.Evaluate(clean, thresholds).Level);
        Assert.Equal(HealthLevel.Warning, rule.Evaluate(errors, thresholds).Level);
    }

    [Fact]
    public void No_Data_Is_Unknown_For_Every_Rule()
    {
        IHealthRule[] rules = [
            new CpuHealthRule(),
            new MemoryHealthRule(),
            new DiskHealthRule(),
            new VolumesHealthRule(),
            new GpuHealthRule(),
            new NetworkHealthRule(),
            new ThermalHealthRule()
        ];

        foreach (IHealthRule rule in rules) {
            Assert.Equal(HealthLevel.Unknown, rule.Evaluate(new HealthSample(), thresholds).Level);
        }
    }

    [Fact]
    public void Settings_Change_The_Result()
    {
        HealthThresholds relaxed = new() { CpuWarningPercent = 95, CpuCriticalPercent = 99 };
        HealthSample     sample  = Cpu(new CpuMetrics { CpuTotalTime = 0.92 });

        Assert.Equal(HealthLevel.Warning, new CpuHealthRule().Evaluate(sample, thresholds).Level);
        Assert.Equal(HealthLevel.Ok, new CpuHealthRule().Evaluate(sample, relaxed).Level);
    }

    private static HealthSample Thermal(ThermalMetrics metrics) =>
        new() { Thermal = new ThermalInfo { Metrics = metrics } };

    [Fact]
    public void Thermal_Ignores_Sensors_With_No_Critical_Limit()
    {
        ThermalMetrics metrics = new();
        metrics.Sensors.Add(new ThermalSensor { Component = ThermalComponent.Disk, Celsius = 99.0 });

        HealthReading reading = new ThermalHealthRule().Evaluate(Thermal(metrics), thresholds);

        Assert.Equal(HealthLevel.Ok, reading.Level);
        Assert.Contains("99", reading.Utilisation);
    }

    [Theory]
    [InlineData(ThermalPressure.Nominal, HealthLevel.Ok)]
    [InlineData(ThermalPressure.Moderate, HealthLevel.Warning)]
    [InlineData(ThermalPressure.Heavy, HealthLevel.Critical)]
    [InlineData(ThermalPressure.Trapping, HealthLevel.Critical)]
    public void Thermal_Is_Judged_On_Pressure(ThermalPressure pressure, HealthLevel expected)
    {
        HealthReading reading = new ThermalHealthRule().Evaluate(Thermal(new ThermalMetrics { Pressure = pressure }), thresholds);

        Assert.Equal(expected, reading.Level);
    }

    [Theory]
    [InlineData(100.0, HealthLevel.Ok)]
    [InlineData(70.0, HealthLevel.Warning)]
    public void Thermal_Is_Judged_On_The_Cpu_Performance_Limit(double limit, HealthLevel expected)
    {
        HealthReading reading = new ThermalHealthRule().Evaluate(Cpu(new CpuMetrics { PerformanceLimitPercent = limit }), thresholds);

        Assert.Equal(expected, reading.Level);
    }

    [Theory]
    [InlineData(80.0, HealthLevel.Ok)]
    [InlineData(92.0, HealthLevel.Warning)]
    [InlineData(98.0, HealthLevel.Critical)]
    public void Thermal_Is_Judged_On_The_Margin_To_A_Sensors_Limit(double celsius, HealthLevel expected)
    {
        ThermalMetrics metrics = new();
        metrics.Sensors.Add(new ThermalSensor { Component = ThermalComponent.Cpu, Celsius = celsius, CriticalCelsius = 100.0 });

        Assert.Equal(expected, new ThermalHealthRule().Evaluate(Thermal(metrics), thresholds).Level);
    }

    [Theory]
    [InlineData(50L, 100L, false, HealthLevel.Ok)]
    [InlineData(9L, 100L, false, HealthLevel.Warning)]
    [InlineData(4L, 100L, false, HealthLevel.Critical)]
    public void Volumes_Are_Judged_On_Free_Percent(long free, long total, bool isSystem, HealthLevel expected)
    {
        HealthSample sample = new() { Volumes = [new HealthVolume("D:\\", free, total, isSystem)] };

        Assert.Equal(expected, new VolumesHealthRule().Evaluate(sample, thresholds).Level);
    }

    [Fact]
    public void Volumes_Report_The_Fullest_One()
    {
        HealthSample sample = new() {
            Volumes = [
                new HealthVolume("C:\\", 50, 100, true),
                new HealthVolume("D:\\", 20, 100, false)
            ]
        };

        Assert.Equal("D:\\ 80% full", new VolumesHealthRule().Evaluate(sample, thresholds).Utilisation);
    }

    [Fact]
    public void Volumes_System_Drive_Is_Critical_Below_Its_Own_Limit()
    {
        long total = 1024L * (long)Gigabyte;
        long free  = 1L * (long)Gigabyte;

        HealthVolume system = new("C:\\", free * 1, total / 1000, true);
        HealthVolume other  = system with { IsSystem = false };

        // 1 GB free of a 1 GB-ish volume is plenty as a percentage, so only the system limit can fire.
        Assert.Equal(HealthLevel.Critical, new VolumesHealthRule().Evaluate(new HealthSample { Volumes = [system] }, thresholds).Level);
        Assert.Equal(HealthLevel.Ok, new VolumesHealthRule().Evaluate(new HealthSample { Volumes = [other] }, thresholds).Level);
    }
}
