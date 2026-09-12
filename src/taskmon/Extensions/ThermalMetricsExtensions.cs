using Task.Monitor.System.Services.Thermal;

namespace Task.Monitor.Extensions;

public static class ThermalMetricsExtensions
{
    // The one temperature to show for a component. A vendor-SDK reading beats an ACPI zone; among
    // equals the canonical sensor wins (a drive's "Composite", a GPU's "GPU" reading) and only then
    // the hottest - so a hot controller or memory-junction sensor does not hijack the headline.
    public static double? CpuTemperature(this ThermalMetrics metrics) =>
        PrimaryTemperature(metrics, ThermalComponent.Cpu, componentId: null);

    public static double? GpuTemperature(this ThermalMetrics metrics, long adapterLuid) =>
        PrimaryTemperature(metrics, ThermalComponent.Gpu, adapterLuid.ToString());

    public static double? DiskTemperature(this ThermalMetrics metrics, int diskIndex) =>
        PrimaryTemperature(metrics, ThermalComponent.Disk, diskIndex.ToString());

    public static double? PrimaryTemperature(
        this ThermalMetrics metrics, ThermalComponent component, string? componentId)
    {
        ThermalSensor? best = null;

        foreach (ThermalSensor sensor in metrics.Sensors) {
            if (sensor.Component != component) {
                continue;
            }

            if (componentId != null && sensor.ComponentId != componentId) {
                continue;
            }

            if (best is null || IsPreferred(sensor, best)) {
                best = sensor;
            }
        }

        return best?.Celsius;
    }

    private static bool IsPreferred(ThermalSensor candidate, ThermalSensor current)
    {
        int candidateSource = SourceRank(candidate.Source);
        int currentSource = SourceRank(current.Source);

        if (candidateSource != currentSource) {
            return candidateSource > currentSource;
        }

        int candidateName = NameRank(candidate.SensorName);
        int currentName = NameRank(current.SensorName);

        if (candidateName != currentName) {
            return candidateName > currentName;
        }

        return candidate.Celsius > current.Celsius;
    }

    // A stable, human label for a sensor in the thermals list.
    public static string ToDisplayName(this ThermalSensor sensor, string? componentLabel) =>
        string.IsNullOrEmpty(componentLabel)
            ? sensor.SensorName
            : $"{componentLabel} — {sensor.SensorName}";

    public static string ToTemperatureText(this double celsius) => $"{celsius:0}°C";

    public static string ToTemperatureText(this double? celsius) =>
        celsius is { } value ? value.ToTemperatureText() : "N/A";

    private static int SourceRank(ThermalSource source) => source switch {
        ThermalSource.KernelMsr => 4,
        ThermalSource.NvApi or ThermalSource.Adl or ThermalSource.Igcl => 3,
        ThermalSource.NvmeHealthLog or ThermalSource.AtaSmart => 3,
        ThermalSource.Battery => 2,
        ThermalSource.AcpiThermalZone => 1,
        _ => 0
    };

    private static int NameRank(string sensorName) => sensorName switch {
        "Composite" or "GPU" or "Drive" or "Package" or "Edge" => 1,
        _ => 0
    };
}
