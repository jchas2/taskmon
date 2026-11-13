using System.Globalization;
using Task.Monitor.System.Services.Disk;

namespace Task.Monitor.System.Services.Health.Rules;

internal sealed class DiskHealthRule : IHealthRule
{
    private const double CriticalWarningSeverity = 2.0;

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

    public HealthReading Evaluate(HealthSample sample, HealthThresholds thresholds)
    {
        if (sample.Disk?.Metrics is not { } metrics) {
            return new HealthReading { Resource = Resource };
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

        HealthCondition worst = HealthCondition.Worst(
            sample.Drives.Select(drive => DriveCondition(drive, thresholds)).Append(speed));

        return new HealthReading {
            Resource = Resource,
            Level    = worst.Level,
            Cause    = worst.Cause,
            Severity = worst.Severity,
            Measures = Measures(sample, metrics)
        };
    }

    private static DiskHealthMeasures Measures(HealthSample sample, DiskMetrics metrics) =>
        new(
            metrics.PercentActiveTime,
            metrics.LatencyMs,
            metrics.QueueLength,
            sample.Drives.Count > 0 ? sample.Drives.Max(drive => drive.PercentUsed) : null,
            sample.Drives.Aggregate(0UL, (total, drive) => total + drive.MediaErrors),
            sample.Reports.Count(report => report.Kind == HealthReportKind.DiskError));

    public HealthResource Resource => HealthResource.Disk;
}
