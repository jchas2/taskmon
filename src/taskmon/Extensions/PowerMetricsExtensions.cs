using Task.Monitor.System.Services.Power;

namespace Task.Monitor.Extensions;

public static class PowerMetricsExtensions
{
    public static double? GpuPower(this PowerMetrics metrics, long adapterLuid) =>
        Match(metrics, PowerComponent.Gpu, adapterLuid.ToString())?.Watts;

    // NVMe drives report a nameplate peak, not a live draw; the reading carries IsRated so the
    // caller can label it.
    public static PowerReading? DiskPower(this PowerMetrics metrics, int diskIndex) =>
        Match(metrics, PowerComponent.Disk, diskIndex.ToString());

    public static double? SystemPower(this PowerMetrics metrics) =>
        Match(metrics, PowerComponent.System, componentId: null)?.Watts;

    private static PowerReading? Match(PowerMetrics metrics, PowerComponent component, string? componentId)
    {
        foreach (PowerReading reading in metrics.Readings) {
            if (reading.Component != component) {
                continue;
            }

            if (componentId != null && reading.ComponentId != componentId) {
                continue;
            }

            return reading;
        }

        return null;
    }

    public static string ToWattText(this double watts) => $"{watts:0.0} W";

    public static string ToWattText(this double? watts) =>
        watts is { } value ? value.ToWattText() : "N/A";
}
