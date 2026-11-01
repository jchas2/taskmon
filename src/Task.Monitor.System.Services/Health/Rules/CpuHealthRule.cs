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

        // Clamped as sample timing can put this a little over 100%.
        double busyPercent = Math.Min(metrics.CpuTotalTime * PercentPerRatio, PercentPerRatio);

        List<HealthCondition> conditions = [
            HealthCondition.Rising(
                busyPercent,
                thresholds.CpuWarningPercent,
                thresholds.CpuCriticalPercent,
                $"{busyPercent:0}% busy")
        ];

        if (metrics.RunQueuePerCore is { } queue) {
            conditions.Add(HealthCondition.Rising(
                queue,
                thresholds.RunQueueWarningPerCore,
                double.PositiveInfinity,
                $"{queue:0.0} threads waiting per core"));
        }

        if (metrics.DriverTimePercent is { } driverTime) {
            conditions.Add(HealthCondition.Rising(
                driverTime,
                thresholds.DriverTimeWarningPercent,
                double.PositiveInfinity,
                $"{driverTime:0}% of CPU in drivers"));
        }

        HealthCondition worst = HealthCondition.Worst(conditions);

        return new HealthReading {
            Resource    = Resource,
            Level       = worst.Level,
            Reason      = worst.Reason,
            Severity    = worst.Severity,
            Utilisation = $"{busyPercent:0}%",
            Saturation  = metrics.RunQueuePerCore is { } perCore ? $"Queue {perCore:0.0} per core" : string.Empty
        };
    }

    public HealthResource Resource => HealthResource.Cpu;
}
