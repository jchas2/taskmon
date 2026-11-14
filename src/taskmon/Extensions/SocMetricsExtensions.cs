using System.Globalization;
using Task.Monitor.Resources;
using Task.Monitor.System.Services.Soc;

namespace Task.Monitor.Extensions;

public static class SocMetricsExtensions
{
    private const double LimitThreshold = 0.01;

    public static string ToGpuFrequencyText(this SocMetrics metrics) =>
        metrics.GpuFrequencyMhz is { } mhz ? $"{mhz:N0} MHz" : Strings.SocGpuIdle;

    public static string ToGpuLimitText(this SocMetrics metrics)
    {
        List<string> limits = new();

        if (metrics.GpuThermalLimitRatio is >= LimitThreshold and var thermal) {
            limits.Add(string.Format(CultureInfo.CurrentCulture, Strings.SocThermalLimit, thermal * 100));
        }

        if (metrics.GpuPowerLimitRatio is >= LimitThreshold and var power) {
            limits.Add(string.Format(CultureInfo.CurrentCulture, Strings.SocPowerLimit, power * 100));
        }

        return limits.Count > 0 ? string.Join(" · ", limits) : Strings.SocNoLimit;
    }
}
