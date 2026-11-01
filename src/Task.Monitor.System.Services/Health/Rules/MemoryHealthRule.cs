using Task.Monitor.System.Services.Memory;

namespace Task.Monitor.System.Services.Health.Rules;

internal sealed class MemoryHealthRule : IHealthRule
{
    private const double BytesPerMegabyte = 1024.0 * 1024.0;
    private const double PercentPerRatio  = 100.0;

    private static HealthCondition AvailableCondition(double availablePercent, HealthThresholds thresholds) =>
        HealthCondition.Falling(
            availablePercent,
            thresholds.MemoryAvailableWarningPercent,
            thresholds.MemoryAvailableCriticalPercent,
            $"{availablePercent:0}% available");

    private static double? CommitPercent(MemoryMetrics metrics) =>
        metrics.CommitLimit > 0.0
            ? metrics.CommitTotal / metrics.CommitLimit * PercentPerRatio
            : null;

    public HealthReading Evaluate(HealthSample sample, HealthThresholds thresholds)
    {
        if (sample.Memory?.Metrics is not { TotalPhysical: > 0 } metrics) {
            return new HealthReading { Resource = Resource };
        }

        double availablePercent = metrics.AvailablePhysical / (double)metrics.TotalPhysical * PercentPerRatio;
        double? commitPercent   = CommitPercent(metrics);

        HealthCondition worst = metrics.Pressure is { } pressure
            ? PressureCondition(pressure, metrics, thresholds)
            : NumericCondition(availablePercent, commitPercent, metrics, thresholds);

        return new HealthReading {
            Resource    = Resource,
            Level       = worst.Level,
            Reason      = worst.Reason,
            Severity    = worst.Severity,
            Utilisation = $"{100.0 - availablePercent:0}% in use",
            Saturation  = SaturationText(metrics, commitPercent)
        };
    }

    private static HealthCondition NumericCondition(
        double           availablePercent,
        double?          commitPercent,
        MemoryMetrics    metrics,
        HealthThresholds thresholds)
    {
        List<HealthCondition> conditions = [AvailableCondition(availablePercent, thresholds)];

        if (commitPercent is { } commit) {
            conditions.Add(HealthCondition.Rising(
                commit,
                thresholds.CommitWarningPercent,
                thresholds.CommitCriticalPercent,
                $"commit {commit:0}%"));
        }

        // Windows reads pages back in after memory comes free, and programs starting up read their
        // files the same way, so hard faults only point at a shortage when memory is short.
        if (metrics.HardFaultsPerSecond is { } hardFaults && availablePercent <= thresholds.HardFaultsAvailablePercent) {
            conditions.Add(HealthCondition.Rising(
                hardFaults,
                thresholds.HardFaultsWarningPerSecond,
                double.PositiveInfinity,
                $"{hardFaults:0} hard faults/s"));
        }

        return HealthCondition.Worst(conditions);
    }

    // macOS memory is judged by the kernel's own pressure level. Available memory is low by
    // design, as the system fills spare memory with cache.
    private static HealthCondition PressureCondition(
        MemoryPressure   pressure,
        MemoryMetrics    metrics,
        HealthThresholds thresholds)
    {
        HealthCondition level = pressure switch {
            MemoryPressure.Critical => new HealthCondition(HealthLevel.Critical, "kernel pressure critical", 2.0),
            MemoryPressure.Warning  => new HealthCondition(HealthLevel.Warning, "kernel pressure warning", 1.0),
                                  _ => HealthCondition.Clear
        };

        double swapOutMb = (metrics.SwapOutBytesPerSecond ?? 0.0) / BytesPerMegabyte;

        HealthCondition swap = swapOutMb > thresholds.SwapOutWarningMbPerSecond
            ? new HealthCondition(HealthLevel.Warning, $"swapping out {swapOutMb:0.0} MB/s", 1.0 + swapOutMb)
            : HealthCondition.Clear;

        return HealthCondition.Worst([level, swap]);
    }

    public HealthResource Resource => HealthResource.Memory;

    private static string SaturationText(MemoryMetrics metrics, double? commitPercent)
    {
        if (metrics.Pressure is { } pressure) {
            return $"Pressure {pressure}";
        }

        if (metrics.HardFaultsPerSecond is not { } hardFaults) {
            return commitPercent is { } commit ? $"Commit {commit:0}%" : string.Empty;
        }

        return commitPercent is { } percent
            ? $"Commit {percent:0}%, {hardFaults:0} faults/s"
            : $"Hard faults {hardFaults:0}/s";
    }
}
