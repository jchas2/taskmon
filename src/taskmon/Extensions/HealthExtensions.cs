using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Services.Health;
using Task.Monitor.System.Services.Memory;
using Task.Monitor.System.Services.Thermal;

namespace Task.Monitor.Extensions;

public static class HealthExtensions
{
    private const double BytesPerMegabyte = 1024.0 * 1024.0;
    private const double HoursPerDay      = 24.0;
    private const double MaxDaysToShow    = 30.0;
    private const double MinutesPerHour   = 60.0;
    private const double NoLimitPercent   = 100.0;
    private const double NotChangingBytes = 1024.0 * 1024.0;

    private static readonly (byte Bit, string Text)[] DriveWarnings = [
        (0x01, "spare space low"),
        (0x02, "over temperature"),
        (0x04, "reliability degraded"),
        (0x08, "read-only"),
        (0x10, "backup failed")
    ];

    private static string AppsErrorsText(int faults) => faults switch {
        0 => string.Empty,
        1 => "1 app fault",
        _ => $"{faults} app faults"
    };

    private static string DescribeDriveWarning(double bits) =>
        string.Join(", ", DriveWarnings.Where(item => ((byte)bits & item.Bit) != 0).Select(item => item.Text));

    private static string DescribeTimeLeft(double minutes)
    {
        if (minutes < MinutesPerHour) {
            return $"{Math.Max(1.0, minutes):0} min";
        }

        double hours = minutes / MinutesPerHour;

        return hours < HoursPerDay
            ? $"{hours:0} h"
            : $"{hours / HoursPerDay:0} days";
    }

    private static string DiskErrorsText(DiskHealthMeasures disk)
    {
        List<string> parts = [];

        if (disk.WornPercent is { } worn) {
            parts.Add($"{worn}% worn");
        }

        if (disk.MediaErrors > 0) {
            parts.Add($"{disk.MediaErrors} media");
        }

        if (disk.LoggedErrors > 0) {
            parts.Add($"{disk.LoggedErrors} logged");
        }

        return string.Join(", ", parts);
    }

    private static string DiskName(string index) => $"Disk {index}";

    private static string DiskSaturationText(DiskHealthMeasures disk)
    {
        if (disk.LatencyMs is not { } latency) {
            return string.Empty;
        }

        return disk.QueueLength is > 0.0 and var queue
            ? $"Latency {latency:0.0} ms, queue {queue:0}"
            : $"Latency {latency:0.0} ms";
    }

    private static string FullText(string volume, double fullPercent) => $"{volume} {fullPercent:0}% full";

    private static string GpuMemoryName(bool isDedicated) => isDedicated ? "VRAM" : "Memory";

    private static string GpuSaturationText(GpuHealthMeasures gpu) =>
        gpu.MemoryUsedPercent is { } used
            ? $"{GpuMemoryName(gpu.IsDedicatedMemory)} {used:0}% used"
            : string.Empty;

    public static bool IsShownInUtilisation(this HealthCause cause) => cause.Kind == HealthCauseKind.VolumeFull;

    private static string MemorySaturationText(MemoryHealthMeasures memory)
    {
        if (memory.Pressure is { } pressure) {
            return $"Pressure {pressure}";
        }

        if (memory.PagesInPerSecond is not { } pagesIn) {
            return memory.CommitPercent is { } commit ? $"Commit {commit:0}%" : string.Empty;
        }

        return memory.CommitPercent is { } percent
            ? $"Commit {percent:0}%, {PagesInText(pagesIn)}"
            : PagesInText(pagesIn);
    }

    private static string PagesInText(double pagesIn) => $"{pagesIn:0} pages in/s";

    private static string SensorText(string component, double celsius) =>
        Enum.TryParse(component, out ThermalComponent parsed)
            ? SensorText(parsed, celsius)
            : $"{component} {celsius:0} °C";

    private static string SensorText(ThermalComponent component, double celsius)
    {
        string name = component is ThermalComponent.Cpu or ThermalComponent.Gpu
            ? component.ToString().ToUpperInvariant()
            : component.ToString();

        return $"{name} {celsius:0} °C";
    }

    private static string ThermalSaturationText(ThermalHealthMeasures thermal)
    {
        if (thermal.Pressure is { } level) {
            return $"Pressure {level}";
        }

        if (thermal.PerformanceLimitPercent is not { } percent) {
            return string.Empty;
        }

        return Math.Round(percent) >= NoLimitPercent
            ? "CPU not limited"
            : $"CPU limited to {percent:0}%";
    }

    public static string ToDetailText(this HealthReport report) => report.Cause switch {
        HealthReportCause.AppError             => report.Module.Length > 0 ? $"{report.App}, in {report.Module}" : report.App,
        HealthReportCause.AppHang              => report.App,
        HealthReportCause.BadBlock             => "Bad block",
        HealthReportCause.CpuLimitReport       => $"{report.App}: Used too much CPU",
        HealthReportCause.CrashReport          => $"{report.App}: Crashed",
        HealthReportCause.DeviceReset          => $"{report.Driver} reset the device",
        HealthReportCause.DiskWriteLimitReport => $"{report.App}: Wrote too much to disk",
        HealthReportCause.HangReport           => $"{report.App}: Stopped responding",
        HealthReportCause.HardwareError        => $"WHEA event {report.EventId}",
        HealthReportCause.JetsamReport         => "macOS stopped processes to free memory",
        HealthReportCause.PagingError          => "Error during a paging operation",
        HealthReportCause.RequestRetried       => "Request retried",
        HealthReportCause.SpinReport           => $"{report.App}: Spun without responding",
        HealthReportCause.UnexpectedRestart    => "Restarted without shutting down cleanly",
        HealthReportCause.VirtualMemoryLow     => "Windows ran low on virtual memory",
                                             _ => report.App
    };

    public static string ToErrorsText(this HealthMeasures? measures) => measures switch {
        AppsHealthMeasures apps       => AppsErrorsText(apps.Faults),
        DiskHealthMeasures disk       => DiskErrorsText(disk),
        NetworkHealthMeasures network => $"{network.ErrorsPerSecond:0.#} errors/s",
                                    _ => string.Empty
    };

    public static string ToReasonText(this HealthCause? cause)
    {
        if (cause == null) {
            return string.Empty;
        }

        double value = cause.Value;

        return cause.Kind switch {
            HealthCauseKind.AppNotResponding    => $"{cause.Subject} not responding for {value:0} s",
            HealthCauseKind.CommitCharge        => $"commit {value:0}%",
            HealthCauseKind.CpuBusy             => $"{value:0}% busy",
            HealthCauseKind.CpuPerformanceLimit => $"CPU limited to {value:0}%",
            HealthCauseKind.DiskActive          => $"{value:0}% active",
            HealthCauseKind.DiskLatency         => $"latency {value:0} ms",
            HealthCauseKind.DriverTime          => $"{value:0}% of CPU in drivers",
            HealthCauseKind.DriveWarning        => $"{DiskName(cause.Subject)}: {DescribeDriveWarning(value)}",
            HealthCauseKind.DriveWear           => $"{DiskName(cause.Subject)} {value:0}% worn",
            HealthCauseKind.GpuHeatLimit        => $"held back by heat {value:0}%",
            HealthCauseKind.GpuMemoryFree       => $"{GpuMemoryName(false)} {value:0}% free",
            HealthCauseKind.GpuPowerLimit       => $"held back by power {value:0}%",
            HealthCauseKind.MemoryAvailable     => $"{value:0}% available",
            HealthCauseKind.MemoryPressure      => $"kernel pressure {((MemoryPressure)(int)value).ToString().ToLowerInvariant()}",
            HealthCauseKind.NetworkDiscards     => $"{value:0.#} discards/s",
            HealthCauseKind.NetworkErrors       => $"{value:0.#} errors/s",
            HealthCauseKind.PagesIn             => PagesInText(value),
            HealthCauseKind.RunQueue            => $"queue {value:0.0} per core",
            HealthCauseKind.SensorNearLimit     => $"{SensorText(cause.Subject, value)} near its {cause.Limit:0} °C limit",
            HealthCauseKind.SwapOut             => $"swapping out {value.ToFormattedByteRate()}",
            HealthCauseKind.ThermalPressure     => $"thermal pressure {(ThermalPressure)(int)value}",
            HealthCauseKind.VolumeFull          => FullText(cause.Subject, value),
            HealthCauseKind.VramFree            => $"{GpuMemoryName(true)} {value:0}% free",
                                              _ => string.Empty
        };
    }

    public static string ToSaturationText(this HealthMeasures? measures) => measures switch {
        AppsHealthMeasures apps       => apps.NotResponding is { } count ? $"{count} not responding" : string.Empty,
        CpuHealthMeasures cpu         => cpu.RunQueuePerCore is { } queue ? $"Queue {queue:0.0} per core" : string.Empty,
        DiskHealthMeasures disk       => DiskSaturationText(disk),
        GpuHealthMeasures gpu         => GpuSaturationText(gpu),
        MemoryHealthMeasures memory   => MemorySaturationText(memory),
        NetworkHealthMeasures network => $"Discards {network.DiscardsPerSecond:0.#}/s",
        ThermalHealthMeasures thermal => ThermalSaturationText(thermal),
        VolumesHealthMeasures volume  => VolumeTrendText(volume),
                                    _ => string.Empty
    };

    public static string ToTitle(this HealthReport report) => report.Kind switch {
        HealthReportKind.AppCrash          => "App crash",
        HealthReportKind.AppHang           => "App hang",
        HealthReportKind.DiskError         => "Disk error",
        HealthReportKind.HardwareError     => "Hardware error",
        HealthReportKind.LowMemory         => "Low memory",
        HealthReportKind.ResourceLimit     => report.Cause == HealthReportCause.CpuLimitReport ? "CPU limit" : "Disk write limit",
        HealthReportKind.UnexpectedRestart => "Unexpected restart",
                                         _ => report.Kind.ToString()
    };

    public static string ToUsageText(this HealthCulprit culprit)
    {
        if (culprit.NotRespondingSeconds is { } seconds) {
            return $"Not responding for {seconds:0} s";
        }

        List<string> parts = [];

        if (culprit.CpuPercent is { } cpu) {
            parts.Add($"{cpu:0}% CPU");
        }

        if (culprit.MemoryBytes is { } memory) {
            parts.Add($"{memory.ToFormattedByteSize()} memory");
        }

        if (culprit.MemoryGrowthBytesPerMinute is { } growth) {
            parts.Add($"+{growth / BytesPerMegabyte:0} MB/min");
        }

        if (culprit.DiskBytesPerSecond is { } disk) {
            parts.Add($"{disk / BytesPerMegabyte:0} MB/s disk");
        }

        if (culprit.GpuPercent is { } gpu) {
            parts.Add($"{gpu:0}% GPU");
        }

        return string.Join(", ", parts);
    }

    public static string ToUtilisationText(this HealthMeasures? measures) => measures switch {
        CpuHealthMeasures cpu         => $"{cpu.BusyPercent:0}%",
        DiskHealthMeasures disk       => $"{disk.ActivePercent:0}% active",
        GpuHealthMeasures gpu         => $"{gpu.BusyPercent:0}%",
        MemoryHealthMeasures memory   => $"{memory.InUsePercent:0}% in use",
        NetworkHealthMeasures network => $"↓ {network.ReceiveBytesPerSecond.ToFormattedByteRate()} ↑ {network.SendBytesPerSecond.ToFormattedByteRate()}",
        ThermalHealthMeasures thermal => thermal.HottestCelsius is { } celsius ? SensorText(thermal.HottestComponent, celsius) : string.Empty,
        VolumesHealthMeasures volume  => FullText(volume.Name, volume.FullPercent),
                                    _ => string.Empty
    };

    private static string VolumeTrendText(VolumesHealthMeasures volume)
    {
        if (volume.FillBytesPerMinute is not { } rate) {
            return string.Empty;
        }

        if (Math.Abs(rate) < NotChangingBytes) {
            return "Not changing";
        }

        string speed = $"{((long)Math.Abs(rate)).ToFormattedByteSize()}/min";

        if (rate < 0.0) {
            return $"Freeing {speed}";
        }

        double minutesLeft = volume.FreeBytes / rate;

        return minutesLeft <= MaxDaysToShow * HoursPerDay * MinutesPerHour
            ? $"Filling {speed}, about {DescribeTimeLeft(minutesLeft)} left"
            : $"Filling {speed}";
    }
}
