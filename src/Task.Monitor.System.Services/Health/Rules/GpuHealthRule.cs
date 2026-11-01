using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Soc;

namespace Task.Monitor.System.Services.Health.Rules;

internal sealed class GpuHealthRule : IHealthRule
{
    private const double LimitThreshold  = 0.01;
    private const double PercentPerRatio = 100.0;

    public HealthReading Evaluate(HealthSample sample, HealthThresholds thresholds)
    {
        if (sample.Gpu?.Metrics is not { } metrics) {
            return new HealthReading { Resource = Resource };
        }

        double? freePercent = metrics.TotalCombinedGpuMemory > 0
            ? metrics.AvailableCombinedGpuMemory / (double)metrics.TotalCombinedGpuMemory * PercentPerRatio
            : null;

        List<HealthCondition> conditions = [LimitCondition(sample.Soc?.Metrics)];

        if (freePercent is { } free) {
            conditions.Add(HealthCondition.Falling(
                free,
                thresholds.GpuMemoryFreeWarningPercent,
                double.NegativeInfinity,
                $"{free:0}% memory free"));
        }

        HealthCondition worst = HealthCondition.Worst(conditions);

        return new HealthReading {
            Resource    = Resource,
            Level       = worst.Level,
            Reason      = worst.Reason,
            Severity    = worst.Severity,
            Utilisation = $"{metrics.GpuPercentTime * PercentPerRatio:0}%",
            Saturation  = freePercent is { } percent ? $"Memory {100.0 - percent:0}%" : string.Empty
        };
    }

    private static HealthCondition LimitCondition(SocMetrics? metrics)
    {
        double thermal = metrics?.GpuThermalLimitRatio ?? 0.0;
        double power   = metrics?.GpuPowerLimitRatio ?? 0.0;
        double limit   = Math.Max(thermal, power);

        if (limit < LimitThreshold) {
            return HealthCondition.Clear;
        }

        string cause = thermal >= power 
            ? "heat" 
            : "power";
        
        return new HealthCondition(HealthLevel.Warning, $"held back by {cause} {limit * PercentPerRatio:0}%", 1.0 + limit);
    }

    public HealthResource Resource => HealthResource.Gpu;
}
