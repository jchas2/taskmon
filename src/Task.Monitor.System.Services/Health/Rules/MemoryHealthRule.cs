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
            new HealthCause(HealthCauseKind.MemoryAvailable, availablePercent));

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
            Resource = Resource,
            Level    = worst.Level,
            Cause    = worst.Cause,
            Severity = worst.Severity,
            Measures = new MemoryHealthMeasures(
                PercentPerRatio - availablePercent,
                commitPercent,
                metrics.PagesInPerSecond,
                metrics.Pressure)
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
                new HealthCause(HealthCauseKind.CommitCharge, commit)));
        }

        // Windows reads pages back in after memory comes free, and programs starting up read their
        // files the same way, so hard faults only point at a shortage when memory is short.
        if (metrics.PagesInPerSecond is { } pagesIn && availablePercent <= thresholds.HardFaultsAvailablePercent) {
            conditions.Add(HealthCondition.Rising(
                pagesIn,
                thresholds.HardFaultsWarningPerSecond,
                double.PositiveInfinity,
                new HealthCause(HealthCauseKind.PagesIn, pagesIn)));
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

    public HealthResource Resource => HealthResource.Memory;
}
