using System.Globalization;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Memory;
using Task.Monitor.System.Services.Soc;
using Task.Monitor.System.Services.Thermal;

namespace Task.Monitor.System.Services.Health.Rules;

internal static class HealthRules
{
    private const double BytesPerGigabyte        = 1024.0 * 1024.0 * 1024.0;
    private const double BytesPerMegabyte        = 1024.0 * 1024.0;
    private const double CriticalWarningSeverity = 2.0;
    private const double GpuLimitThreshold       = 0.01;
    private const double PercentPerRatio         = 100.0;

    private sealed record GpuMemory(bool IsDedicated, double FreePercent);

    public static HealthReading Apps(HealthSample sample, HealthThresholds thresholds)
    {
        int faults = sample.Reports.Count(report => report.Kind is HealthReportKind.AppCrash or HealthReportKind.AppHang);

        if (!sample.CanDetectHungApps) {
            return new HealthReading { Resource = HealthResource.Apps, Measures = new AppsHealthMeasures(null, faults) };
        }

        HungApp? longest = sample.HungApps.MaxBy(app => app.Seconds);

        HealthCondition condition = longest != null
            ? HealthCondition.Rising(
                longest.Seconds,
                thresholds.NotRespondingWarningSeconds,
                thresholds.NotRespondingCriticalSeconds,
                new HealthCause(HealthCauseKind.AppNotResponding, longest.Seconds, longest.Name))
            : HealthCondition.Clear;

        return Reading(HealthResource.Apps, condition, new AppsHealthMeasures(sample.HungApps.Count, faults), isImmediate: true);
    }

    private static double? CommitPercent(MemoryMetrics metrics) =>
        metrics.CommitLimit > 0.0
            ? metrics.CommitTotal / metrics.CommitLimit * PercentPerRatio
            : null;

    public static HealthReading Cpu(HealthSample sample, HealthThresholds thresholds)
    {
        if (sample.Cpu?.Metrics is not { } metrics) {
            return new HealthReading { Resource = HealthResource.Cpu };
        }

        double busyPercent = Math.Min(metrics.CpuTotalTime * PercentPerRatio, PercentPerRatio);

        List<HealthCondition> conditions = [
            HealthCondition.Rising(
                busyPercent,
                thresholds.CpuWarningPercent,
                thresholds.CpuCriticalPercent,
                new HealthCause(HealthCauseKind.CpuBusy, busyPercent))
        ];

        if (metrics.RunQueuePerCore is { } queue && busyPercent >= thresholds.RunQueueBusyPercent) {
            conditions.Add(HealthCondition.Rising(
                queue,
                thresholds.RunQueueWarningPerCore,
                double.PositiveInfinity,
                new HealthCause(HealthCauseKind.RunQueue, queue)));
        }

        if (metrics.DriverTimePercent is { } driverTime) {
            conditions.Add(HealthCondition.Rising(
                driverTime,
                thresholds.DriverTimeWarningPercent,
                double.PositiveInfinity,
                new HealthCause(HealthCauseKind.DriverTime, driverTime)));
        }

        return Reading(
            HealthResource.Cpu,
            HealthCondition.Worst(conditions),
            new CpuHealthMeasures(busyPercent, metrics.RunQueuePerCore));
    }

    public static HealthReading Disk(HealthSample sample, HealthThresholds thresholds)
    {
        if (sample.Disk?.Metrics is not { } metrics) {
            return new HealthReading { Resource = HealthResource.Disk };
        }

        HealthCondition speed = metrics.LatencyMs is { } latency
            ? HealthCondition.Rising(
                latency,
                thresholds.DiskLatencyWarningMs,
                thresholds.DiskLatencyCriticalMs,
                new HealthCause(HealthCauseKind.DiskLatency, latency))
            : HealthCondition.Rising(
                metrics.PercentActiveTime,
                thresholds.DiskActiveWarningPercent,
                double.PositiveInfinity,
                new HealthCause(HealthCauseKind.DiskActive, metrics.PercentActiveTime));

        DiskHealthMeasures measures = new(
            metrics.PercentActiveTime,
            metrics.LatencyMs,
            metrics.QueueLength,
            sample.Drives.Count > 0 ? sample.Drives.Max(drive => drive.PercentUsed) : null,
            sample.Drives.Aggregate(0UL, (total, drive) => total + drive.MediaErrors),
            sample.Reports.Count(report => report.Kind == HealthReportKind.DiskError));

        return Reading(
            HealthResource.Disk,
            HealthCondition.Worst(sample.Drives.Select(drive => DriveCondition(drive, thresholds)).Append(speed)),
            measures);
    }

    private static HealthCondition DriveCondition(DriveHealth drive, HealthThresholds thresholds)
    {
        string index = drive.Index.ToString(CultureInfo.InvariantCulture);

        if (drive.CriticalWarning != 0) {
            return new HealthCondition(
                HealthLevel.Critical,
                new HealthCause(HealthCauseKind.DriveWarning, drive.CriticalWarning, index),
                CriticalWarningSeverity);
        }

        return HealthCondition.Rising(
            drive.PercentUsed,
            thresholds.NvmeWearWarningPercent,
            double.PositiveInfinity,
            new HealthCause(HealthCauseKind.DriveWear, drive.PercentUsed, index));
    }

    private static double FreePercent(long available, long total) =>
        available / (double)total * PercentPerRatio;

    private static double FreePercent(HealthVolume volume) =>
        volume.TotalBytes > 0
            ? volume.FreeBytes / (double)volume.TotalBytes * PercentPerRatio
            : PercentPerRatio;

    public static HealthReading Gpu(HealthSample sample, HealthThresholds thresholds)
    {
        if (sample.Gpu?.Metrics is not { } metrics) {
            return new HealthReading { Resource = HealthResource.Gpu };
        }

        GpuSpecs specs = sample.Gpu.Specs;

        GpuMemory? memory = metrics.Devices
            .Where(device => device.TotalCombinedGpuMemory > 0)
            .Select(device => ReadGpuMemory(device, specs))
            .MinBy(reading => reading.FreePercent);

        List<HealthCondition> conditions = [GpuLimitCondition(sample.Soc?.Metrics)];

        if (memory != null) {
            HealthCauseKind kind = memory.IsDedicated
                ? HealthCauseKind.VramFree
                : HealthCauseKind.GpuMemoryFree;

            conditions.Add(HealthCondition.Falling(
                memory.FreePercent,
                thresholds.GpuMemoryFreeWarningPercent,
                double.NegativeInfinity,
                new HealthCause(kind, memory.FreePercent)));
        }

        return Reading(
            HealthResource.Gpu,
            HealthCondition.Worst(conditions),
            new GpuHealthMeasures(
                metrics.GpuPercentTime * PercentPerRatio,
                PercentPerRatio - memory?.FreePercent,
                memory?.IsDedicated ?? false));
    }

    private static HealthCondition GpuLimitCondition(SocMetrics? metrics)
    {
        double thermal = metrics?.GpuThermalLimitRatio ?? 0.0;
        double power   = metrics?.GpuPowerLimitRatio ?? 0.0;
        double limit   = Math.Max(thermal, power);

        if (limit < GpuLimitThreshold) {
            return HealthCondition.Clear;
        }

        HealthCauseKind kind = thermal >= power
            ? HealthCauseKind.GpuHeatLimit
            : HealthCauseKind.GpuPowerLimit;

        return new HealthCondition(HealthLevel.Warning, new HealthCause(kind, limit * PercentPerRatio), 1.0 + limit);
    }

    public static HealthReading Memory(HealthSample sample, HealthThresholds thresholds)
    {
        if (sample.Memory?.Metrics is not { TotalPhysical: > 0 } metrics) {
            return new HealthReading { Resource = HealthResource.Memory };
        }

        double availablePercent = metrics.AvailablePhysical / (double)metrics.TotalPhysical * PercentPerRatio;
        double? commitPercent   = CommitPercent(metrics);

        HealthCondition worst = metrics.Pressure is { } pressure
            ? MemoryPressureCondition(pressure, metrics, thresholds)
            : MemoryNumericCondition(availablePercent, commitPercent, metrics, thresholds);

        return Reading(
            HealthResource.Memory,
            worst,
            new MemoryHealthMeasures(
                PercentPerRatio - availablePercent,
                commitPercent,
                metrics.PagesInPerSecond,
                metrics.Pressure));
    }

    private static HealthCondition MemoryNumericCondition(
        double           availablePercent,
        double?          commitPercent,
        MemoryMetrics    metrics,
        HealthThresholds thresholds)
    {
        List<HealthCondition> conditions = [
            HealthCondition.Falling(
                availablePercent,
                thresholds.MemoryAvailableWarningPercent,
                thresholds.MemoryAvailableCriticalPercent,
                new HealthCause(HealthCauseKind.MemoryAvailable, availablePercent))
        ];

        if (commitPercent is { } commit) {
            conditions.Add(HealthCondition.Rising(
                commit,
                thresholds.CommitWarningPercent,
                thresholds.CommitCriticalPercent,
                new HealthCause(HealthCauseKind.CommitCharge, commit)));
        }

        if (metrics.PagesInPerSecond is { } pagesIn && availablePercent <= thresholds.HardFaultsAvailablePercent) {
            conditions.Add(HealthCondition.Rising(
                pagesIn,
                thresholds.HardFaultsWarningPerSecond,
                double.PositiveInfinity,
                new HealthCause(HealthCauseKind.PagesIn, pagesIn)));
        }

        return HealthCondition.Worst(conditions);
    }

    private static HealthCondition MemoryPressureCondition(
        MemoryPressure   pressure,
        MemoryMetrics    metrics,
        HealthThresholds thresholds)
    {
        HealthCause cause = new(HealthCauseKind.MemoryPressure, (int)pressure);

        HealthCondition level = pressure switch {
            MemoryPressure.Critical => new HealthCondition(HealthLevel.Critical, cause, 2.0),
            MemoryPressure.Warning  => new HealthCondition(HealthLevel.Warning, cause, 1.0),
                                  _ => HealthCondition.Clear
        };

        double swapOut   = metrics.SwapOutBytesPerSecond ?? 0.0;
        double swapOutMb = swapOut / BytesPerMegabyte;

        HealthCondition swap = swapOutMb > thresholds.SwapOutWarningMbPerSecond
            ? new HealthCondition(HealthLevel.Warning, new HealthCause(HealthCauseKind.SwapOut, swapOut), 1.0 + swapOutMb)
            : HealthCondition.Clear;

        return HealthCondition.Worst([level, swap]);
    }

    public static HealthReading Network(HealthSample sample, HealthThresholds thresholds)
    {
        if (sample.Network?.Metrics is not { } metrics) {
            return new HealthReading { Resource = HealthResource.Network };
        }

        HealthCondition RateCondition(double perSecond, HealthCauseKind kind) =>
            perSecond > 0.0
                ? new HealthCondition(HealthLevel.Warning, new HealthCause(kind, perSecond), 1.0 + perSecond)
                : HealthCondition.Clear;

        return Reading(
            HealthResource.Network,
            HealthCondition.Worst([
                RateCondition(metrics.ErrorsPerSecond, HealthCauseKind.NetworkErrors),
                RateCondition(metrics.DiscardsPerSecond, HealthCauseKind.NetworkDiscards)
            ]),
            new NetworkHealthMeasures(
                metrics.ReceiveBytesPerSecond,
                metrics.SendBytesPerSecond,
                metrics.DiscardsPerSecond,
                metrics.ErrorsPerSecond));
    }

    private static GpuMemory ReadGpuMemory(GpuDeviceMetrics device, GpuSpecs specs)
    {
        bool isDiscrete = specs.Devices.Any(spec => spec.AdapterLuid == device.AdapterLuid && spec.IsDiscrete);

        return isDiscrete
            ? new GpuMemory(true, FreePercent(device.AvailableGpuMemory, device.TotalGpuMemory))
            : new GpuMemory(false, FreePercent(device.AvailableCombinedGpuMemory, device.TotalCombinedGpuMemory));
    }

    private static HealthReading Reading(
        HealthResource  resource,
        HealthCondition condition,
        HealthMeasures  measures,
        bool            isImmediate = false) =>
        new() {
            Resource    = resource,
            Level       = condition.Level,
            Cause       = condition.Cause,
            Severity    = condition.Severity,
            IsImmediate = isImmediate,
            Measures    = measures
        };

    public static HealthReading Thermal(HealthSample sample, HealthThresholds thresholds)
    {
        ThermalMetrics? metrics = sample.Thermal?.Metrics;
        double?         limit   = sample.Cpu?.Metrics.PerformanceLimitPercent;

        if (metrics == null && limit == null) {
            return new HealthReading { Resource = HealthResource.Thermal };
        }

        List<HealthCondition> conditions = [];

        foreach (ThermalSensor sensor in metrics?.Sensors ?? []) {
            if (sensor.CriticalCelsius is { } critical) {
                conditions.Add(HealthCondition.Falling(
                    critical - sensor.Celsius,
                    thresholds.SensorWarningMarginCelsius,
                    thresholds.SensorCriticalMarginCelsius,
                    new HealthCause(HealthCauseKind.SensorNearLimit, sensor.Celsius, sensor.Component.ToString(), critical)));
            }
        }

        if (metrics?.Pressure is { } pressure) {
            conditions.Add(ThermalPressureCondition(pressure, thresholds));
        }

        if (limit is { } percent) {
            conditions.Add(HealthCondition.Falling(
                percent,
                thresholds.PerformanceLimitWarningPercent,
                double.NegativeInfinity,
                new HealthCause(HealthCauseKind.CpuPerformanceLimit, percent)));
        }

        ThermalSensor? hottest = metrics?.Sensors.MaxBy(sensor => sensor.Celsius);

        return Reading(
            HealthResource.Thermal,
            HealthCondition.Worst(conditions),
            new ThermalHealthMeasures(
                hottest?.Component ?? ThermalComponent.Other,
                hottest?.Celsius,
                metrics?.Pressure,
                limit));
    }

    private static HealthCondition ThermalPressureCondition(ThermalPressure pressure, HealthThresholds thresholds)
    {
        if (pressure < thresholds.ThermalPressureWarning) {
            return HealthCondition.Clear;
        }

        HealthLevel level = pressure >= thresholds.ThermalPressureCritical
            ? HealthLevel.Critical
            : HealthLevel.Warning;

        return new HealthCondition(level, new HealthCause(HealthCauseKind.ThermalPressure, (int)pressure), 1.0 + (int)pressure);
    }

    private static HealthCondition VolumeCondition(HealthVolume volume, HealthThresholds thresholds)
    {
        double      freePercent = FreePercent(volume);
        HealthCause cause       = new(HealthCauseKind.VolumeFull, PercentPerRatio - freePercent, volume.Name);

        HealthCondition byPercent = HealthCondition.Falling(
            freePercent,
            thresholds.VolumeFreeWarningPercent,
            thresholds.VolumeFreeCriticalPercent,
            cause);

        if (!volume.IsSystem) {
            return byPercent;
        }

        HealthCondition byBytes = HealthCondition.Falling(
            volume.FreeBytes / BytesPerGigabyte,
            double.NegativeInfinity,
            thresholds.SystemVolumeFreeCriticalGb,
            cause);

        return HealthCondition.Worst([byPercent, byBytes]);
    }

    public static HealthReading Volumes(HealthSample sample, HealthThresholds thresholds)
    {
        if (sample.Volumes.Count == 0) {
            return new HealthReading { Resource = HealthResource.Volumes };
        }

        (HealthVolume Volume, HealthCondition Condition)[] judged = sample.Volumes
            .Select(volume => (volume, VolumeCondition(volume, thresholds)))
            .ToArray();

        HealthCondition worst = HealthCondition.Worst(judged.Select(item => item.Condition));

        HealthVolume shown = worst.Level >= HealthLevel.Warning
            ? judged.First(item => item.Condition == worst).Volume
            : sample.Volumes.MinBy(FreePercent)!;

        return Reading(
            HealthResource.Volumes,
            worst,
            new VolumesHealthMeasures(
                shown.Name,
                PercentPerRatio - FreePercent(shown),
                shown.FreeBytes,
                shown.FillBytesPerMinute),
            isImmediate: true);
    }
}
