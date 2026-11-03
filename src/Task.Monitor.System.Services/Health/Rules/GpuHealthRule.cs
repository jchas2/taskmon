using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Soc;

namespace Task.Monitor.System.Services.Health.Rules;

internal sealed class GpuHealthRule : IHealthRule
{
    private const double LimitThreshold  = 0.01;
    private const double PercentPerRatio = 100.0;

    private sealed record GpuMemory(string Name, double FreePercent);

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
            conditions.Add(HealthCondition.Falling(
                memory.FreePercent,
                thresholds.GpuMemoryFreeWarningPercent,
                double.NegativeInfinity,
                $"{memory.Name} {memory.FreePercent:0}% free"));
        }

        HealthCondition worst = HealthCondition.Worst(conditions);

        return new HealthReading {
            Resource    = Resource,
            Level       = worst.Level,
            Reason      = worst.Reason,
            Severity    = worst.Severity,
            Utilisation = $"{metrics.GpuPercentTime * PercentPerRatio:0}%",
            Saturation  = memory != null ? $"{memory.Name} {PercentPerRatio - memory.FreePercent:0}% used" : string.Empty
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

        string cause = thermal >= power 
            ? "heat" 
            : "power";
        
        return new HealthCondition(HealthLevel.Warning, $"held back by {cause} {limit * PercentPerRatio:0}%", 1.0 + limit);
    }

    private static GpuMemory ReadMemory(GpuDeviceMetrics device, GpuSpecs specs)
    {
        bool isDiscrete = specs.Devices.Any(spec => spec.AdapterLuid == device.AdapterLuid && spec.IsDiscrete);

        return isDiscrete
            ? new GpuMemory("VRAM", FreePercent(device.AvailableGpuMemory, device.TotalGpuMemory))
            : new GpuMemory("Memory", FreePercent(device.AvailableCombinedGpuMemory, device.TotalCombinedGpuMemory));
    }

    public HealthResource Resource => HealthResource.Gpu;
}
