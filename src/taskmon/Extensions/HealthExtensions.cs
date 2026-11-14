using System.Globalization;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Resources;
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

    private static string AppsErrorsText(int faults) => faults switch {
        0 => string.Empty,
        1 => Strings.HealthAppFaultOne,
        _ => Format(Strings.HealthAppFaults, faults)
    };

    private static string DescribeDriveWarning(double bits) =>
        string.Join(", ", DriveWarnings.Where(item => ((byte)bits & item.Bit) != 0).Select(item => item.Text));

    private static string DescribeTimeLeft(double minutes)
    {
        if (minutes < MinutesPerHour) {
            return Format(Strings.HealthTimeLeftMinutes, Math.Max(1.0, minutes));
        }

        double hours = minutes / MinutesPerHour;

        return hours < HoursPerDay
            ? Format(Strings.HealthTimeLeftHours, hours)
            : Format(Strings.HealthTimeLeftDays, hours / HoursPerDay);
    }

    private static string DiskErrorsText(DiskHealthMeasures disk)
    {
        List<string> parts = [];

        if (disk.WornPercent is { } worn) {
            parts.Add(Format(Strings.HealthDiskWorn, worn));
        }

        if (disk.MediaErrors > 0) {
            parts.Add(Format(Strings.HealthDiskMediaErrors, disk.MediaErrors));
        }

        if (disk.LoggedErrors > 0) {
            parts.Add(Format(Strings.HealthDiskLoggedErrors, disk.LoggedErrors));
        }

        return string.Join(", ", parts);
    }

    private static string DiskName(string index) => Format(Strings.HealthDiskName, index);

    private static string DiskSaturationText(DiskHealthMeasures disk)
    {
        if (disk.LatencyMs is not { } latency) {
            return string.Empty;
        }

        return disk.QueueLength is > 0.0 and var queue
            ? Format(Strings.HealthLatencyQueue, latency, queue)
            : Format(Strings.HealthLatency, latency);
    }

    private static (byte Bit, string Text)[] DriveWarnings => [
        (0x01, Strings.HealthDriveSpareLow),
        (0x02, Strings.HealthDriveOverTemperature),
        (0x04, Strings.HealthDriveReliabilityDegraded),
        (0x08, Strings.HealthDriveReadOnly),
        (0x10, Strings.HealthDriveBackupFailed)
    ];

    private static string Format(string format, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, format, args);

    private static string FullText(string volume, double fullPercent) => Format(Strings.HealthVolumeFull, volume, fullPercent);

    private static string GpuMemoryName(bool isDedicated) => isDedicated ? Strings.HealthVram : Strings.HealthGpuMemory;

    private static string GpuSaturationText(GpuHealthMeasures gpu) =>
        gpu.MemoryUsedPercent is { } used
            ? Format(Strings.HealthGpuMemoryUsed, GpuMemoryName(gpu.IsDedicatedMemory), used)
            : string.Empty;

    public static bool IsShownInUtilisation(this HealthCause cause) => cause.Kind == HealthCauseKind.VolumeFull;

    private static string MemorySaturationText(MemoryHealthMeasures memory)
    {
        if (memory.Pressure is { } pressure) {
            return Format(Strings.HealthPressureLevel, pressure.ToDisplayText());
        }

        if (memory.PagesInPerSecond is not { } pagesIn) {
            return memory.CommitPercent is { } commit ? Format(Strings.HealthCommit, commit) : string.Empty;
        }

        return memory.CommitPercent is { } percent
            ? Format(Strings.HealthCommitPagesIn, percent, PagesInText(pagesIn))
            : PagesInText(pagesIn);
    }

    private static string PagesInText(double pagesIn) => Format(Strings.HealthPagesIn, pagesIn);

    private static string SensorText(string component, double celsius) =>
        Enum.TryParse(component, out ThermalComponent parsed)
            ? SensorText(parsed, celsius)
            : Format(Strings.HealthSensorTemperature, component, celsius);

    private static string SensorText(ThermalComponent component, double celsius) =>
        Format(Strings.HealthSensorTemperature, component.ToDisplayText(), celsius);

    private static string ThermalSaturationText(ThermalHealthMeasures thermal)
    {
        if (thermal.Pressure is { } level) {
            return Format(Strings.HealthPressureLevel, level.ToDisplayText());
        }

        if (thermal.PerformanceLimitPercent is not { } percent) {
            return string.Empty;
        }

        return Math.Round(percent) >= NoLimitPercent
            ? Strings.HealthCpuNotLimited
            : Format(Strings.HealthCpuLimited, percent);
    }

    public static string ToDetailText(this HealthReport report) => report.Cause switch {
        HealthReportCause.AppError             => report.Module.Length > 0 ? Format(Strings.HealthReportAppErrorIn, report.App, report.Module) : report.App,
        HealthReportCause.AppHang              => report.App,
        HealthReportCause.BadBlock             => Strings.HealthReportBadBlock,
        HealthReportCause.CpuLimitReport       => Format(Strings.HealthReportCpuLimit, report.App),
        HealthReportCause.CrashReport          => Format(Strings.HealthReportCrash, report.App),
        HealthReportCause.DeviceReset          => Format(Strings.HealthReportDeviceReset, report.Driver),
        HealthReportCause.DiskWriteLimitReport => Format(Strings.HealthReportDiskWriteLimit, report.App),
        HealthReportCause.HangReport           => Format(Strings.HealthReportHang, report.App),
        HealthReportCause.HardwareError        => Format(Strings.HealthReportHardwareError, report.EventId),
        HealthReportCause.JetsamReport         => Strings.HealthReportJetsam,
        HealthReportCause.PagingError          => Strings.HealthReportPagingError,
        HealthReportCause.RequestRetried       => Strings.HealthReportRequestRetried,
        HealthReportCause.SpinReport           => Format(Strings.HealthReportSpin, report.App),
        HealthReportCause.UnexpectedRestart    => Strings.HealthReportUnexpectedRestart,
        HealthReportCause.VirtualMemoryLow     => Strings.HealthReportVirtualMemoryLow,
                                             _ => report.App
    };

    public static string ToErrorsText(this HealthMeasures? measures) => measures switch {
        AppsHealthMeasures apps       => AppsErrorsText(apps.Faults),
        DiskHealthMeasures disk       => DiskErrorsText(disk),
        NetworkHealthMeasures network => Format(Strings.HealthErrorsPerSecond, network.ErrorsPerSecond),
                                    _ => string.Empty
    };

    public static string ToReasonText(this HealthCause? cause)
    {
        if (cause == null) {
            return string.Empty;
        }

        double value = cause.Value;

        return cause.Kind switch {
            HealthCauseKind.AppNotResponding    => Format(Strings.HealthReasonNotResponding, cause.Subject, value),
            HealthCauseKind.CommitCharge        => Format(Strings.HealthReasonCommit, value),
            HealthCauseKind.CpuBusy             => Format(Strings.HealthReasonBusy, value),
            HealthCauseKind.CpuPerformanceLimit => Format(Strings.HealthCpuLimited, value),
            HealthCauseKind.DiskActive          => Format(Strings.HealthReasonActive, value),
            HealthCauseKind.DiskLatency         => Format(Strings.HealthReasonLatency, value),
            HealthCauseKind.DriverTime          => Format(Strings.HealthReasonDriverTime, value),
            HealthCauseKind.DriveWarning        => $"{DiskName(cause.Subject)}: {DescribeDriveWarning(value)}",
            HealthCauseKind.DriveWear           => Format(Strings.HealthReasonWorn, DiskName(cause.Subject), value),
            HealthCauseKind.GpuHeatLimit        => Format(Strings.HealthReasonHeatLimit, value),
            HealthCauseKind.GpuMemoryFree       => Format(Strings.HealthReasonFree, GpuMemoryName(false), value),
            HealthCauseKind.GpuPowerLimit       => Format(Strings.HealthReasonPowerLimit, value),
            HealthCauseKind.MemoryAvailable     => Format(Strings.HealthReasonAvailable, value),
            HealthCauseKind.MemoryPressure      => Format(Strings.HealthReasonKernelPressure, ((MemoryPressure)(int)value).ToDisplayText().ToLower(CultureInfo.CurrentCulture)),
            HealthCauseKind.NetworkDiscards     => Format(Strings.HealthReasonDiscards, value),
            HealthCauseKind.NetworkErrors       => Format(Strings.HealthErrorsPerSecond, value),
            HealthCauseKind.PagesIn             => PagesInText(value),
            HealthCauseKind.RunQueue            => Format(Strings.HealthReasonQueue, value),
            HealthCauseKind.SensorNearLimit     => Format(Strings.HealthReasonNearLimit, SensorText(cause.Subject, value), cause.Limit),
            HealthCauseKind.SwapOut             => Format(Strings.HealthReasonSwappingOut, value.ToFormattedByteRate()),
            HealthCauseKind.ThermalPressure     => Format(Strings.HealthReasonThermalPressure, ((ThermalPressure)(int)value).ToDisplayText()),
            HealthCauseKind.VolumeFull          => FullText(cause.Subject, value),
            HealthCauseKind.VramFree            => Format(Strings.HealthReasonFree, GpuMemoryName(true), value),
                                              _ => string.Empty
        };
    }

    public static string ToSaturationText(this HealthMeasures? measures) => measures switch {
        AppsHealthMeasures apps       => apps.NotResponding is { } count ? Format(Strings.HealthNotRespondingCount, count) : string.Empty,
        CpuHealthMeasures cpu         => cpu.RunQueuePerCore is { } queue ? Format(Strings.HealthQueuePerCore, queue) : string.Empty,
        DiskHealthMeasures disk       => DiskSaturationText(disk),
        GpuHealthMeasures gpu         => GpuSaturationText(gpu),
        MemoryHealthMeasures memory   => MemorySaturationText(memory),
        NetworkHealthMeasures network => Format(Strings.HealthDiscardsPerSecond, network.DiscardsPerSecond),
        ThermalHealthMeasures thermal => ThermalSaturationText(thermal),
        VolumesHealthMeasures volume  => VolumeTrendText(volume),
                                    _ => string.Empty
    };

    public static string ToTitle(this HealthReport report) => report.Kind switch {
        HealthReportKind.AppCrash          => Strings.HealthTitleAppCrash,
        HealthReportKind.AppHang           => Strings.HealthTitleAppHang,
        HealthReportKind.DiskError         => Strings.HealthTitleDiskError,
        HealthReportKind.HardwareError     => Strings.HealthTitleHardwareError,
        HealthReportKind.LowMemory         => Strings.HealthTitleLowMemory,
        HealthReportKind.ResourceLimit     => report.Cause == HealthReportCause.CpuLimitReport ? Strings.HealthTitleCpuLimit : Strings.HealthTitleDiskWriteLimit,
        HealthReportKind.UnexpectedRestart => Strings.HealthTitleUnexpectedRestart,
                                         _ => report.Kind.ToString()
    };

    public static string ToUsageText(this HealthCulprit culprit)
    {
        if (culprit.NotRespondingSeconds is { } seconds) {
            return Format(Strings.HealthUsageNotResponding, seconds);
        }

        List<string> parts = [];

        if (culprit.CpuPercent is { } cpu) {
            parts.Add(Format(Strings.HealthUsageCpu, cpu));
        }

        if (culprit.MemoryBytes is { } memory) {
            parts.Add(Format(Strings.HealthUsageMemory, memory.ToFormattedByteSize()));
        }

        if (culprit.MemoryGrowthBytesPerMinute is { } growth) {
            parts.Add(Format(Strings.HealthUsageMemoryGrowth, growth / BytesPerMegabyte));
        }

        if (culprit.DiskBytesPerSecond is { } disk) {
            parts.Add(Format(Strings.HealthUsageDisk, disk / BytesPerMegabyte));
        }

        if (culprit.GpuPercent is { } gpu) {
            parts.Add(Format(Strings.HealthUsageGpu, gpu));
        }

        return string.Join(", ", parts);
    }

    public static string ToUtilisationText(this HealthMeasures? measures) => measures switch {
        CpuHealthMeasures cpu         => $"{cpu.BusyPercent:0}%",
        DiskHealthMeasures disk       => Format(Strings.HealthReasonActive, disk.ActivePercent),
        GpuHealthMeasures gpu         => $"{gpu.BusyPercent:0}%",
        MemoryHealthMeasures memory   => Format(Strings.HealthInUse, memory.InUsePercent),
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
            return Strings.HealthNotChanging;
        }

        string speed = Format(Strings.HealthPerMinute, ((long)Math.Abs(rate)).ToFormattedByteSize());

        if (rate < 0.0) {
            return Format(Strings.HealthFreeing, speed);
        }

        double minutesLeft = volume.FreeBytes / rate;

        return minutesLeft <= MaxDaysToShow * HoursPerDay * MinutesPerHour
            ? Format(Strings.HealthFillingTimeLeft, speed, DescribeTimeLeft(minutesLeft))
            : Format(Strings.HealthFilling, speed);
    }
}
