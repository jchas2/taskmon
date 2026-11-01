using Moq;
using Task.Monitor.Configuration;
using Task.Monitor.Internal.Abstractions;
using Task.Monitor.System.Configuration;
using Task.Monitor.System.Services.Health;
using Task.Monitor.System.Services.Thermal;

namespace Task.Monitor.Tests.Configuration;

public sealed class AppConfigHealthTests
{
    private const string ConfigPath = "/dummy/path/taskmon.ini";

    private readonly Mock<IFileSystem> fileSystem = new();

    [Fact]
    public void A_New_Config_Writes_Every_Threshold_With_Its_Default()
    {
        string text = new AppConfig(fileSystem.Object).ToString();

        Assert.Contains("[health]", text);
        Assert.Contains("cpu-warning-percent=90", text);
        Assert.Contains("thermal-pressure-warning=Moderate", text);
        Assert.Contains("performance-limit-warning-percent=80", text);
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

    private AppConfig Load(string health) =>
        new(fileSystem.Object, Config.FromString($"[health]\n{health}\n")!);

    [Fact]
    public void Text_In_A_Number_Falls_Back_To_The_Default()
    {
        Assert.Equal(90, Load("cpu-warning-percent=lots").HealthThresholds.CpuWarningPercent);
    }

    [Fact]
    public void Thresholds_From_The_File_Are_Applied()
    {
        HealthThresholds thresholds = Load(
            "cpu-warning-percent=70\n" +
            "cpu-critical-percent=85\n" +
            "warning-sustain-seconds=10\n" +
            "sustain-grace-seconds=8\n" +
            "hard-faults-available-percent=40\n" +
            "report-lookback-hours=48\n" +
            "not-responding-warning-seconds=7\n" +
            "not-responding-critical-seconds=70\n" +
            "nvme-wear-warning-percent=80\n" +
            "disk-latency-warning-ms=40\n" +
            "disk-latency-critical-ms=200\n" +
            "thermal-pressure-warning=Heavy\n" +
            "thermal-pressure-critical=Trapping").HealthThresholds;

        Assert.Equal(70, thresholds.CpuWarningPercent);
        Assert.Equal(85, thresholds.CpuCriticalPercent);
        Assert.Equal(10, thresholds.WarningSustainSeconds);
        Assert.Equal(8, thresholds.SustainGraceSeconds);
        Assert.Equal(40, thresholds.HardFaultsAvailablePercent);
        Assert.Equal(48, thresholds.ReportLookbackHours);
        Assert.Equal(7, thresholds.NotRespondingWarningSeconds);
        Assert.Equal(70, thresholds.NotRespondingCriticalSeconds);
        Assert.Equal(80, thresholds.NvmeWearWarningPercent);
        Assert.Equal(40, thresholds.DiskLatencyWarningMs);
        Assert.Equal(200, thresholds.DiskLatencyCriticalMs);
        Assert.Equal(ThermalPressure.Heavy, thresholds.ThermalPressureWarning);
        Assert.Equal(ThermalPressure.Trapping, thresholds.ThermalPressureCritical);
    }

    [Fact]
    public void Thresholds_The_Wrong_Way_Round_Fall_Back_To_The_Defaults()
    {
        HealthThresholds thresholds = Load("cpu-warning-percent=99\ncpu-critical-percent=50").HealthThresholds;

        Assert.Equal(new HealthThresholds().CpuWarningPercent, thresholds.CpuWarningPercent);
        Assert.Equal(new HealthThresholds().CpuCriticalPercent, thresholds.CpuCriticalPercent);
    }
}
