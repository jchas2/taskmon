using Task.Monitor.System.Services.Soc;

namespace Task.Monitor.Extensions;

public static class SocMetricsExtensions
{
    private const double LimitThreshold = 0.01;

    public static string ToGpuFrequencyText(this SocMetrics metrics) =>
        metrics.GpuFrequencyMhz is { } mhz ? $"{mhz:N0} MHz" : "Idle";

    public static string ToGpuLimitText(this SocMetrics metrics)
    {
        List<string> limits = new();

        if (metrics.GpuThermalLimitRatio is >= LimitThreshold and var thermal) {
            limits.Add($"Thermal {thermal * 100:0}%");
        }

        if (metrics.GpuPowerLimitRatio is >= LimitThreshold and var power) {
            limits.Add($"Power {power * 100:0}%");
        }

        return limits.Count > 0 ? string.Join(" · ", limits) : "None";
    }
}
