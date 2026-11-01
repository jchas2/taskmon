using Task.Monitor.System.Services.Disk;

namespace Task.Monitor.System.Services.Health.Rules;

internal sealed class DiskHealthRule : IHealthRule
{
    private const double CriticalWarningSeverity = 2.0;

    private static readonly (byte Bit, string Text)[] CriticalWarnings = [
        (0x01, "spare space low"),
        (0x02, "over temperature"),
        (0x04, "reliability degraded"),
        (0x08, "read-only"),
        (0x10, "backup failed")
    ];

    private static string DescribeWarning(byte warning) =>
        string.Join(", ", CriticalWarnings.Where(item => (warning & item.Bit) != 0).Select(item => item.Text));

    private static HealthCondition DriveCondition(DriveHealth drive, HealthThresholds thresholds)
    {
        if (drive.CriticalWarning != 0) {
            return new HealthCondition(
                HealthLevel.Critical,
                $"{drive.Name}: {DescribeWarning(drive.CriticalWarning)}",
                CriticalWarningSeverity);
        }

        return HealthCondition.Rising(
            drive.PercentUsed,
            thresholds.NvmeWearWarningPercent,
            double.PositiveInfinity,
            $"{drive.Name} {drive.PercentUsed}% worn");
    }

    private static string ErrorsText(HealthSample sample)
    {
        List<string> parts = [];

        if (sample.Drives.Count > 0) {
            parts.Add($"{sample.Drives.Max(drive => drive.PercentUsed)}% worn");
        }

        ulong mediaErrors = sample.Drives.Aggregate(0UL, (total, drive) => total + drive.MediaErrors);

        if (mediaErrors > 0) {
            parts.Add($"{mediaErrors} media");
        }

        int reported = sample.Reports.Count(report => report.Kind == HealthReportKind.DiskError);

        if (reported > 0) {
            parts.Add($"{reported} logged");
        }

        return string.Join(", ", parts);
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
                $"latency {latency:0} ms")
            : HealthCondition.Rising(
                metrics.PercentActiveTime,
                thresholds.DiskActiveWarningPercent,
                double.PositiveInfinity,
                $"{metrics.PercentActiveTime:0}% active");

        HealthCondition worst = HealthCondition.Worst(
            sample.Drives.Select(drive => DriveCondition(drive, thresholds)).Append(speed));

        return new HealthReading {
            Resource    = Resource,
            Level       = worst.Level,
            Reason      = worst.Reason,
            Severity    = worst.Severity,
            Utilisation = $"{metrics.PercentActiveTime:0}% active",
            Saturation  = SaturationText(metrics),
            Errors      = ErrorsText(sample)
        };
    }

    public HealthResource Resource => HealthResource.Disk;

    private static string SaturationText(DiskMetrics metrics)
    {
        if (metrics.LatencyMs is not { } latency) {
            return string.Empty;
        }

        return metrics.QueueLength is > 0.0 and var queue
            ? $"Latency {latency:0.0} ms, queue {queue:0}"
            : $"Latency {latency:0.0} ms";
    }
}
