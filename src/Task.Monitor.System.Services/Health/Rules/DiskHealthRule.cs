using Task.Monitor.System.Services.Disk;

namespace Task.Monitor.System.Services.Health.Rules;

internal sealed class DiskHealthRule : IHealthRule
{
    public HealthReading Evaluate(HealthSample sample, HealthThresholds thresholds)
    {
        if (sample.Disk?.Metrics is not { } metrics) {
            return new HealthReading { Resource = Resource };
        }

        // Latency says whether the disk is keeping up. Active time alone doesn't: an SSD can be
        // 100% active with plenty in hand, so it only counts when there is no latency to read.
        HealthCondition worst = metrics.LatencyMs is { } latency
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

        return new HealthReading {
            Resource    = Resource,
            Level       = worst.Level,
            Reason      = worst.Reason,
            Severity    = worst.Severity,
            Utilisation = $"{metrics.PercentActiveTime:0}% active",
            Saturation  = SaturationText(metrics)
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
