using Moq;
using Task.Monitor.Configuration;
using Task.Monitor.Internal.Abstractions;
using Task.Monitor.System.Configuration;
using Task.Monitor.System.Services.Health;
using Task.Monitor.System.Services.Thermal;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.Tests.Configuration;

public sealed class AppConfigHealthTests
{
    private const string ConfigPath = "/dummy/path/taskmon.ini";

    private readonly Mock<IFileSystem> fileSystem = new();

    [Fact]
    public void HealthThresholds_Canary_Test() =>
        Assert.Equal(33, CanaryTestHelper.GetPropertyCount<HealthThresholds>());

    // Every HealthThresholds property, so a new one fails HealthThresholds_Canary_Test until it is
    // added here and to Thresholds_From_The_File_Are_Applied.
    [Fact]
    public void A_New_Config_Writes_Every_Threshold_With_Its_Default()
    {
        string[] lines = new AppConfig(fileSystem.Object).ToString().ReplaceLineEndings("\n").Split('\n');

        string[] expected = [
            "clear-seconds=30",
            "commit-critical-percent=97",
            "commit-warning-percent=90",
            "cpu-critical-percent=98",
            "cpu-warning-percent=90",
            "critical-sustain-seconds=60",
            "disk-active-warning-percent=90",
            "disk-latency-critical-ms=100",
            "disk-latency-warning-ms=20",
            "driver-time-warning-percent=15",
            "event-history-size=100",
            "gpu-memory-free-warning-percent=10",
            "hard-faults-available-percent=25",
            "hard-faults-warning-per-second=500",
            "memory-available-critical-percent=5",
            "memory-available-warning-percent=10",
            "not-responding-critical-seconds=30",
            "not-responding-warning-seconds=5",
            "nvme-wear-warning-percent=90",
            "performance-limit-warning-percent=80",
            "report-lookback-hours=24",
            "run-queue-busy-percent=50",
            "run-queue-warning-per-core=2",
            "sensor-critical-margin-celsius=3",
            "sensor-warning-margin-celsius=10",
            "sustain-grace-seconds=5",
            "swap-out-warning-mb-per-second=0",
            "system-volume-free-critical-gb=2",
            "thermal-pressure-critical=Heavy",
            "thermal-pressure-warning=Moderate",
            "volume-free-critical-percent=5",
            "volume-free-warning-percent=10",
            "warning-sustain-seconds=30"
        ];

        Assert.Contains("[health]", lines);

        foreach (string line in expected) {
            Assert.Contains(line, lines);
        }
    }

    [Fact]
    public void A_New_Health_Section_Is_Added_To_A_File_With_Its_Comment()
    {
        string? saved = null;

        fileSystem.Setup(fs => fs.FileExists(ConfigPath)).Returns(true);
        fileSystem.Setup(fs => fs.ReadAllText(ConfigPath)).Returns("[stats]\ndelay=1000\n");
        fileSystem.Setup(fs => fs.ReplaceAllText(ConfigPath, It.IsAny<string>()))
            .Callback<string, string?>((_, text) => saved = text);

        AppConfig appConfig = new(fileSystem.Object);

        Assert.True(appConfig.TryLoad(ConfigPath));
        Assert.True(appConfig.TrySave(ConfigPath));
        Assert.Contains("; Thresholds for the HEALTH view", saved);
        Assert.Contains("[health]\nwarning-sustain-seconds=30\n", saved);
    }

    [Fact]
    public void Defaults_Match_The_Thresholds_Record()
    {
        AppConfig appConfig = new(fileSystem.Object);

        Assert.Equal(new HealthThresholds(), appConfig.HealthThresholds);
    }

    private AppConfig Load(string health)
    {
        AppConfig appConfig = new(fileSystem.Object);
        Assert.True(appConfig.TryLoad(Config.FromString($"[health]\n{health}\n")!));

        return appConfig;
    }

    [Fact]
    public void Text_In_A_Number_Falls_Back_To_The_Default()
    {
        Assert.Equal(90, Load("cpu-warning-percent=lots").HealthThresholds.CpuWarningPercent);
    }

    // Regression test: the other default paths fall back to the temp folder, where pruning old
    // event files could delete another program's .ini files.
    [Fact]
    public void The_Events_Path_Does_Not_Fall_Back_To_The_Temp_Folder()
    {
        fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string?>())).Returns(false);
        fileSystem.Setup(fs => fs.TryCreateDirectory(It.IsAny<string>())).Returns(false);

        Assert.Null(new AppConfig(fileSystem.Object).DefaultEventsPath);
    }

    // Every value differs from its default, so a threshold that is never read from the file fails.
    [Fact]
    public void Thresholds_From_The_File_Are_Applied()
    {
        HealthThresholds thresholds = Load(
            "clear-seconds=45\n" +
            "commit-critical-percent=95\n" +
            "commit-warning-percent=85\n" +
            "cpu-critical-percent=85\n" +
            "cpu-warning-percent=70\n" +
            "critical-sustain-seconds=90\n" +
            "disk-active-warning-percent=80\n" +
            "disk-latency-critical-ms=200\n" +
            "disk-latency-warning-ms=40\n" +
            "driver-time-warning-percent=20\n" +
            "event-history-size=50\n" +
            "gpu-memory-free-warning-percent=15\n" +
            "hard-faults-available-percent=40\n" +
            "hard-faults-warning-per-second=750\n" +
            "memory-available-critical-percent=4\n" +
            "memory-available-warning-percent=12\n" +
            "not-responding-critical-seconds=70\n" +
            "not-responding-warning-seconds=7\n" +
            "nvme-wear-warning-percent=80\n" +
            "performance-limit-warning-percent=70\n" +
            "report-lookback-hours=48\n" +
            "run-queue-busy-percent=70\n" +
            "run-queue-warning-per-core=3\n" +
            "sensor-critical-margin-celsius=2\n" +
            "sensor-warning-margin-celsius=8\n" +
            "sustain-grace-seconds=8\n" +
            "swap-out-warning-mb-per-second=25\n" +
            "system-volume-free-critical-gb=4\n" +
            "thermal-pressure-critical=Trapping\n" +
            "thermal-pressure-warning=Heavy\n" +
            "volume-free-critical-percent=3\n" +
            "volume-free-warning-percent=15\n" +
            "warning-sustain-seconds=10").HealthThresholds;

        Assert.Equal(45, thresholds.ClearSeconds);
        Assert.Equal(95, thresholds.CommitCriticalPercent);
        Assert.Equal(85, thresholds.CommitWarningPercent);
        Assert.Equal(85, thresholds.CpuCriticalPercent);
        Assert.Equal(70, thresholds.CpuWarningPercent);
        Assert.Equal(90, thresholds.CriticalSustainSeconds);
        Assert.Equal(80, thresholds.DiskActiveWarningPercent);
        Assert.Equal(200, thresholds.DiskLatencyCriticalMs);
        Assert.Equal(40, thresholds.DiskLatencyWarningMs);
        Assert.Equal(20, thresholds.DriverTimeWarningPercent);
        Assert.Equal(50, thresholds.EventHistorySize);
        Assert.Equal(15, thresholds.GpuMemoryFreeWarningPercent);
        Assert.Equal(40, thresholds.HardFaultsAvailablePercent);
        Assert.Equal(750, thresholds.HardFaultsWarningPerSecond);
        Assert.Equal(4, thresholds.MemoryAvailableCriticalPercent);
        Assert.Equal(12, thresholds.MemoryAvailableWarningPercent);
        Assert.Equal(70, thresholds.NotRespondingCriticalSeconds);
        Assert.Equal(7, thresholds.NotRespondingWarningSeconds);
        Assert.Equal(80, thresholds.NvmeWearWarningPercent);
        Assert.Equal(70, thresholds.PerformanceLimitWarningPercent);
        Assert.Equal(48, thresholds.ReportLookbackHours);
        Assert.Equal(70, thresholds.RunQueueBusyPercent);
        Assert.Equal(3, thresholds.RunQueueWarningPerCore);
        Assert.Equal(2, thresholds.SensorCriticalMarginCelsius);
        Assert.Equal(8, thresholds.SensorWarningMarginCelsius);
        Assert.Equal(8, thresholds.SustainGraceSeconds);
        Assert.Equal(25, thresholds.SwapOutWarningMbPerSecond);
        Assert.Equal(4, thresholds.SystemVolumeFreeCriticalGb);
        Assert.Equal(ThermalPressure.Trapping, thresholds.ThermalPressureCritical);
        Assert.Equal(ThermalPressure.Heavy, thresholds.ThermalPressureWarning);
        Assert.Equal(3, thresholds.VolumeFreeCriticalPercent);
        Assert.Equal(15, thresholds.VolumeFreeWarningPercent);
        Assert.Equal(10, thresholds.WarningSustainSeconds);
    }

    [Fact]
    public void Thresholds_The_Wrong_Way_Round_Fall_Back_To_The_Defaults()
    {
        HealthThresholds thresholds = Load("cpu-warning-percent=99\ncpu-critical-percent=50").HealthThresholds;

        Assert.Equal(new HealthThresholds().CpuWarningPercent, thresholds.CpuWarningPercent);
        Assert.Equal(new HealthThresholds().CpuCriticalPercent, thresholds.CpuCriticalPercent);
    }
}
