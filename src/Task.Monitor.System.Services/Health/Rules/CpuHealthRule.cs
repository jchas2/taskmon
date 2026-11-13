using Task.Monitor.System.Services.Cpu;

namespace Task.Monitor.System.Services.Health.Rules;

internal sealed class CpuHealthRule : IHealthRule
{
    private const double PercentPerRatio = 100.0;

    public HealthReading Evaluate(HealthSample sample, HealthThresholds thresholds)
    {
        if (sample.Cpu?.Metrics is not { } metrics) {
            return new HealthReading { Resource = Resource };
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

        HealthCondition worst = HealthCondition.Worst(conditions);

        return new HealthReading {
            Resource = Resource,
            Level    = worst.Level,
            Cause    = worst.Cause,
            Severity = worst.Severity,
            Measures = new CpuHealthMeasures(busyPercent, metrics.RunQueuePerCore)
        };
    }

    public HealthResource Resource => HealthResource.Cpu;
}
