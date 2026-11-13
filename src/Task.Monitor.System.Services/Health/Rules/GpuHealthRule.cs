using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Soc;

namespace Task.Monitor.System.Services.Health.Rules;

internal sealed class GpuHealthRule : IHealthRule
{
    private const double LimitThreshold  = 0.01;
    private const double PercentPerRatio = 100.0;

    private sealed record GpuMemory(bool IsDedicated, double FreePercent);

    public HealthReading Evaluate(HealthSample sample, HealthThresholds thresholds)
    {
        if (sample.Gpu?.Metrics is not { } metrics) {
            return new HealthReading { Resource = Resource };
        }

        GpuSpecs specs = sample.Gpu.Specs;

        GpuMemory? memory = metrics.Devices
            .Where(device => device.TotalCombinedGpuMemory > 0)
            .Select(device => ReadMemory(device, specs))
            .MinBy(reading => reading.FreePercent);

        List<HealthCondition> conditions = [LimitCondition(sample.Soc?.Metrics)];

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

        HealthCondition worst = HealthCondition.Worst(conditions);

        return new HealthReading {
            Resource = Resource,
            Level    = worst.Level,
            Cause    = worst.Cause,
            Severity = worst.Severity,
            Measures = new GpuHealthMeasures(
                metrics.GpuPercentTime * PercentPerRatio,
                PercentPerRatio - memory?.FreePercent,
                memory?.IsDedicated ?? false)
        };
    }

    private static double FreePercent(long available, long total) =>
        available / (double)total * PercentPerRatio;

    private static HealthCondition LimitCondition(SocMetrics? metrics)
    {
        double thermal = metrics?.GpuThermalLimitRatio ?? 0.0;
        double power   = metrics?.GpuPowerLimitRatio ?? 0.0;
        double limit   = Math.Max(thermal, power);

        if (limit < LimitThreshold) {
            return HealthCondition.Clear;
        }

        HealthCauseKind kind = thermal >= power
            ? HealthCauseKind.GpuHeatLimit
            : HealthCauseKind.GpuPowerLimit;

        return new HealthCondition(HealthLevel.Warning, new HealthCause(kind, limit * PercentPerRatio), 1.0 + limit);
    }

    private static GpuMemory ReadMemory(GpuDeviceMetrics device, GpuSpecs specs)
    {
        bool isDiscrete = specs.Devices.Any(spec => spec.AdapterLuid == device.AdapterLuid && spec.IsDiscrete);

        return isDiscrete
            ? new GpuMemory(true, FreePercent(device.AvailableGpuMemory, device.TotalGpuMemory))
            : new GpuMemory(false, FreePercent(device.AvailableCombinedGpuMemory, device.TotalCombinedGpuMemory));
    }

    public HealthResource Resource => HealthResource.Gpu;
}
