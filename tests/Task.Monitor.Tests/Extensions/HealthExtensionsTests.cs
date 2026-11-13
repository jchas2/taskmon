using Task.Monitor.Extensions;
using Task.Monitor.System.Services.Health;
using Task.Monitor.System.Services.Memory;
using Task.Monitor.System.Services.Thermal;

namespace Task.Monitor.Tests.Extensions;

public sealed class HealthExtensionsTests
{
    private const long Gigabyte = 1024L * 1024L * 1024L;
    private const long Megabyte = 1024L * 1024L;

    private static readonly (HealthReport Report, string Title, string Detail)[] Reports = [
        (Report(HealthReportCause.AppError, app: "chrome.exe", module: "ntdll.dll"), "App crash", "chrome.exe, in ntdll.dll"),
        (Report(HealthReportCause.AppError, app: "powershell.exe"), "App crash", "powershell.exe"),
        (Report(HealthReportCause.AppHang, app: "notepad.exe"), "App hang", "notepad.exe"),
        (Report(HealthReportCause.BadBlock, driver: "disk"), "Disk error", "Bad block"),
        (Report(HealthReportCause.PagingError, driver: "disk"), "Disk error", "Error during a paging operation"),
        (Report(HealthReportCause.RequestRetried, driver: "disk"), "Disk error", "Request retried"),
        (Report(HealthReportCause.DeviceReset, driver: "stornvme"), "Disk error", "stornvme reset the device"),
        (Report(HealthReportCause.HardwareError, eventId: 17), "Hardware error", "WHEA event 17"),
        (Report(HealthReportCause.UnexpectedRestart), "Unexpected restart", "Restarted without shutting down cleanly"),
        (Report(HealthReportCause.VirtualMemoryLow), "Low memory", "Windows ran low on virtual memory"),
        (Report(HealthReportCause.JetsamReport), "Low memory", "macOS stopped processes to free memory"),
        (Report(HealthReportCause.CpuLimitReport, app: "backupd"), "CPU limit", "backupd: Used too much CPU"),
        (Report(HealthReportCause.DiskWriteLimitReport, app: "mds"), "Disk write limit", "mds: Wrote too much to disk"),
        (Report(HealthReportCause.CrashReport, app: "Safari"), "App crash", "Safari: Crashed"),
        (Report(HealthReportCause.HangReport, app: "Google Chrome"), "App hang", "Google Chrome: Stopped responding"),
        (Report(HealthReportCause.SpinReport, app: "Xcode"), "App hang", "Xcode: Spun without responding")
    ];

    public static TheoryData<HealthCause, string> Causes() =>
        new() {
            { new HealthCause(HealthCauseKind.AppNotResponding, 12.0, "notepad"), "notepad not responding for 12 s" },
            { new HealthCause(HealthCauseKind.CommitCharge, 93.0), "commit 93%" },
            { new HealthCause(HealthCauseKind.CpuBusy, 100.0), "100% busy" },
            { new HealthCause(HealthCauseKind.CpuPerformanceLimit, 61.0), "CPU limited to 61%" },
            { new HealthCause(HealthCauseKind.DiskActive, 95.0), "95% active" },
            { new HealthCause(HealthCauseKind.DiskLatency, 30.4), "latency 30 ms" },
            { new HealthCause(HealthCauseKind.DriverTime, 20.0), "20% of CPU in drivers" },
            { new HealthCause(HealthCauseKind.DriveWarning, 0x05, "0"), "Disk 0: spare space low, reliability degraded" },
            { new HealthCause(HealthCauseKind.DriveWear, 91.0, "1"), "Disk 1 91% worn" },
            { new HealthCause(HealthCauseKind.GpuHeatLimit, 25.0), "held back by heat 25%" },
            { new HealthCause(HealthCauseKind.GpuMemoryFree, 4.2), "Memory 4% free" },
            { new HealthCause(HealthCauseKind.GpuPowerLimit, 25.0), "held back by power 25%" },
            { new HealthCause(HealthCauseKind.MemoryAvailable, 9.0), "9% available" },
            { new HealthCause(HealthCauseKind.MemoryPressure, (int)MemoryPressure.Critical), "kernel pressure critical" },
            { new HealthCause(HealthCauseKind.MemoryPressure, (int)MemoryPressure.Warning), "kernel pressure warning" },
            { new HealthCause(HealthCauseKind.NetworkDiscards, 2.5), "2.5 discards/s" },
            { new HealthCause(HealthCauseKind.NetworkErrors, 1.5), "1.5 errors/s" },
            { new HealthCause(HealthCauseKind.PagesIn, 600.0), "600 pages in/s" },
            { new HealthCause(HealthCauseKind.RunQueue, 2.5), "queue 2.5 per core" },
            { new HealthCause(HealthCauseKind.SensorNearLimit, 94.0, "Cpu", 100.0), "CPU 94 °C near its 100 °C limit" },
            { new HealthCause(HealthCauseKind.SensorNearLimit, 80.0, "Battery", 85.0), "Battery 80 °C near its 85 °C limit" },
            { new HealthCause(HealthCauseKind.SwapOut, 5.0 * Megabyte), "swapping out 5.0 MB/s" },
            { new HealthCause(HealthCauseKind.ThermalPressure, (int)ThermalPressure.Moderate), "thermal pressure Moderate" },
            { new HealthCause(HealthCauseKind.VolumeFull, 97.5, "C:\\"), "C:\\ 98% full" },
            { new HealthCause(HealthCauseKind.VramFree, 4.2), "VRAM 4% free" }
        };

    public static TheoryData<HealthMeasures?, string> Errors() =>
        new() {
            { null, string.Empty },
            { new AppsHealthMeasures(null, 0), string.Empty },
            { new AppsHealthMeasures(null, 1), "1 app fault" },
            { new AppsHealthMeasures(0, 3), "3 app faults" },
            { new CpuHealthMeasures(12.0, null), string.Empty },
            { new DiskHealthMeasures(0.0, null, null, 3, 12, 1), "3% worn, 12 media, 1 logged" },
            { new DiskHealthMeasures(0.0, null, null, null, 0, 0), string.Empty },
            { new NetworkHealthMeasures(0.0, 0.0, 2.5, 0.0), "0 errors/s" }
        };

    [Fact]
    public void IsShownInUtilisation_Is_Only_True_For_A_Full_Volume()
    {
        Assert.True(new HealthCause(HealthCauseKind.VolumeFull, 95.0, "E:\\").IsShownInUtilisation());
        Assert.False(new HealthCause(HealthCauseKind.CpuBusy, 95.0).IsShownInUtilisation());
    }

    private static HealthReport Report(
        HealthReportCause cause,
        string            app     = "",
        string            module  = "",
        string            driver  = "",
        int               eventId = 0) =>
        new() {
            TimeUtc = DateTime.UtcNow,
            Cause   = cause,
            App     = app,
            Module  = module,
            Driver  = driver,
            EventId = eventId
        };

    public static TheoryData<HealthReport, string> ReportDetails()
    {
        TheoryData<HealthReport, string> data = new();

        foreach ((HealthReport report, _, string detail) in Reports) {
            data.Add(report, detail);
        }

        return data;
    }

    public static TheoryData<HealthReport, string> ReportTitles()
    {
        TheoryData<HealthReport, string> data = new();

        foreach ((HealthReport report, string title, _) in Reports) {
            data.Add(report, title);
        }

        return data;
    }

    public static TheoryData<HealthMeasures?, string> Saturations() =>
        new() {
            { null, string.Empty },
            { new AppsHealthMeasures(2, 0), "2 not responding" },
            { new AppsHealthMeasures(null, 3), string.Empty },
            { new CpuHealthMeasures(16.0, 2.5), "Queue 2.5 per core" },
            { new CpuHealthMeasures(16.0, null), string.Empty },
            { new DiskHealthMeasures(5.0, null, null, null, 0, 0), string.Empty },
            { new DiskHealthMeasures(5.0, 4.5, 0.0, null, 0, 0), "Latency 4.5 ms" },
            { new DiskHealthMeasures(5.0, 4.5, 2.0, null, 0, 0), "Latency 4.5 ms, queue 2" },
            { new GpuHealthMeasures(10.0, 26.2, false), "Memory 26% used" },
            { new GpuHealthMeasures(10.0, 95.8, true), "VRAM 96% used" },
            { new GpuHealthMeasures(10.0, null, false), string.Empty },
            { new MemoryHealthMeasures(50.0, null, 0.0, null), "0 pages in/s" },
            { new MemoryHealthMeasures(50.0, 31.0, null, null), "Commit 31%" },
            { new MemoryHealthMeasures(50.0, null, null, null), string.Empty },
            { new MemoryHealthMeasures(99.0, null, 600.0, MemoryPressure.Warning), "Pressure Warning" },
            { new NetworkHealthMeasures(0.0, 0.0, 2.5, 0.0), "Discards 2.5/s" },
            { new ThermalHealthMeasures(ThermalComponent.Other, null, ThermalPressure.Moderate, 94.0), "Pressure Moderate" },
            { new ThermalHealthMeasures(ThermalComponent.Other, null, null, null), string.Empty }
        };

    [Theory]
    [MemberData(nameof(ReportDetails))]
    public void ToDetailText_Describes_Each_Report(HealthReport report, string expected)
    {
        Assert.Equal(expected, report.ToDetailText());
    }

    [Theory]
    [MemberData(nameof(Errors))]
    public void ToErrorsText_Describes_Each_Resource(HealthMeasures? measures, string expected)
    {
        Assert.Equal(expected, measures.ToErrorsText());
    }

    [Theory]
    [MemberData(nameof(Causes))]
    public void ToReasonText_Describes_Each_Cause(HealthCause cause, string expected)
    {
        Assert.Equal(expected, cause.ToReasonText());
    }

    [Fact]
    public void ToReasonText_Is_Empty_Without_A_Cause()
    {
        Assert.Empty(((HealthCause?)null).ToReasonText());
    }

    // The swap out rate was written in MB/s, so a small but steady rate read "0.0 MB/s".
    [Fact]
    public void ToReasonText_Shows_A_Small_Swap_Out_Rate_In_KB_Per_Second()
    {
        Assert.Equal("swapping out 400.0 KB/s", new HealthCause(HealthCauseKind.SwapOut, 400.0 * 1024).ToReasonText());
    }

    [Theory]
    [InlineData(0.0, "Not changing")]
    [InlineData(512.0 * 1024, "Not changing")]
    [InlineData(-512.0 * 1024, "Not changing")]
    [InlineData(-40.0 * Megabyte, "Freeing 40.0 MB/min")]
    [InlineData(120.0 * Megabyte, "Filling 120.0 MB/min, about 6 h left")]
    [InlineData(1440.0 * Megabyte, "Filling 1.4 GB/min, about 30 min left")]
    [InlineData(10.0 * Megabyte, "Filling 10.0 MB/min, about 3 days left")]
    public void ToSaturationText_Describes_A_Volumes_Trend(double rate, string expected)
    {
        VolumesHealthMeasures volume = new("E:\\", 57.8, 43200L * Megabyte, rate);

        Assert.Equal(expected, volume.ToSaturationText());
    }

    [Theory]
    [MemberData(nameof(Saturations))]
    public void ToSaturationText_Describes_Each_Resource(HealthMeasures? measures, string expected)
    {
        Assert.Equal(expected, measures.ToSaturationText());
    }

    [Fact]
    public void ToSaturationText_Has_No_Volume_Trend_Until_There_Is_Enough_History()
    {
        Assert.Empty(new VolumesHealthMeasures("E:\\", 50.0, 50L * Gigabyte, null).ToSaturationText());
    }

    [Fact]
    public void ToSaturationText_Leaves_Out_A_Time_Left_Too_Far_Off_To_Mean_Anything()
    {
        VolumesHealthMeasures volume = new("E:\\", 57.8, 432_000L * Megabyte, 2.0 * Megabyte);

        Assert.Equal("Filling 2.0 MB/min", volume.ToSaturationText());
    }

    // Regression test: pages read from disk were shown as "hard faults/s", overstating the faults
    // as each one usually reads several pages.
    [Fact]
    public void ToSaturationText_Names_Pages_Read_In_As_Pages()
    {
        Assert.Equal("Commit 31%, 600 pages in/s", new MemoryHealthMeasures(80.0, 31.0, 600.0, null).ToSaturationText());
    }

    // Regression test: "CPU limit 100%" read as fully limited, when 100 means nothing is held back.
    [Theory]
    [InlineData(100.0, "CPU not limited")]
    [InlineData(99.7, "CPU not limited")]
    [InlineData(94.0, "CPU limited to 94%")]
    public void ToSaturationText_Says_Whether_The_Cpu_Is_Limited(double limit, string expected)
    {
        Assert.Equal(expected, new ThermalHealthMeasures(ThermalComponent.Other, null, null, limit).ToSaturationText());
    }

    [Theory]
    [MemberData(nameof(ReportTitles))]
    public void ToTitle_Names_Each_Report(HealthReport report, string expected)
    {
        Assert.Equal(expected, report.ToTitle());
    }

    [Fact]
    public void ToUsageText_Lists_Only_What_Was_Measured()
    {
        HealthCulprit culprit = new() { ProcessName = "holder", MemoryBytes = 54L * Gigabyte };

        Assert.Equal("54.0 GB memory", culprit.ToUsageText());
    }

    [Fact]
    public void ToUsageText_Lists_What_A_Process_Is_Using()
    {
        HealthCulprit culprit = new() {
            ProcessName                = "miner",
            CpuPercent                 = 45.2,
            MemoryBytes                = 6L * Gigabyte,
            MemoryGrowthBytesPerMinute = 60.0 * Megabyte,
            DiskBytesPerSecond         = 3.0 * Megabyte,
            GpuPercent                 = 20.0
        };

        Assert.Equal("45% CPU, 6.0 GB memory, +60 MB/min, 3 MB/s disk, 20% GPU", culprit.ToUsageText());
    }

    [Fact]
    public void ToUsageText_Says_How_Long_A_Hung_App_Has_Not_Responded()
    {
        HealthCulprit culprit = new() { ProcessName = "notepad", NotRespondingSeconds = 12.4 };

        Assert.Equal("Not responding for 12 s", culprit.ToUsageText());
    }

    [Theory]
    [MemberData(nameof(Utilisations))]
    public void ToUtilisationText_Describes_Each_Resource(HealthMeasures? measures, string expected)
    {
        Assert.Equal(expected, measures.ToUtilisationText());
    }

    // Regression test: the rate was written in MB/s to one decimal place, so traffic under about
    // 51 KB/s read "0.0" and the network looked idle while the Summary charts showed it moving.
    [Fact]
    public void ToUtilisationText_Drops_A_Low_Network_Rate_To_KB_Per_Second()
    {
        NetworkHealthMeasures network = new(20_480.0, 3_072.0, 0.0, 0.0);

        Assert.Equal("↓ 20.0 KB/s ↑ 3.0 KB/s", network.ToUtilisationText());
    }

    [Fact]
    public void ToUtilisationText_Shows_Each_Network_Direction_In_Its_Own_Unit()
    {
        NetworkHealthMeasures network = new(5.0 * Megabyte, 512.0 * 1024, 0.0, 0.0);

        Assert.Equal("↓ 5.0 MB/s ↑ 512.0 KB/s", network.ToUtilisationText());
    }

    public static TheoryData<HealthMeasures?, string> Utilisations() =>
        new() {
            { null, string.Empty },
            { new AppsHealthMeasures(2, 0), string.Empty },
            { new CpuHealthMeasures(100.0, null), "100%" },
            { new DiskHealthMeasures(5.0, null, null, null, 0, 0), "5% active" },
            { new GpuHealthMeasures(37.4, null, false), "37%" },
            { new MemoryHealthMeasures(91.0, null, null, null), "91% in use" },
            { new ThermalHealthMeasures(ThermalComponent.Disk, 99.0, null, null), "Disk 99 °C" },
            { new ThermalHealthMeasures(ThermalComponent.Cpu, 75.4, null, null), "CPU 75 °C" },
            { new ThermalHealthMeasures(ThermalComponent.Other, null, null, 94.0), string.Empty },
            { new VolumesHealthMeasures("D:\\", 80.0, 200L * Gigabyte, null), "D:\\ 80% full" }
        };
}
