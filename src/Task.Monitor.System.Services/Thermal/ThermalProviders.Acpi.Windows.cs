#if __WIN32__
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Thermal;

// CPU / chassis temperatures from WMI's MSAcpi_ThermalZoneTemperature - the only source available
// without a kernel driver. On many desktops it exposes nothing, and where it does the zone is
// often not the CPU die, so readings are surfaced honestly as "ACPI <zone>".
internal sealed class AcpiThermalZoneProvider : IThermalProvider
{
    private const string Namespace = @"root\WMI";
    private const string Wql =
        "SELECT InstanceName, CurrentTemperature, CriticalTripPoint FROM MSAcpi_ThermalZoneTemperature";

    // The WMI round trip costs tens of milliseconds, so a result is reused for a couple of seconds.
    private static readonly TimeSpan CacheWindow = TimeSpan.FromMilliseconds(2500);

    private bool available;
    private DateTime lastQueryUtc;
    private List<ThermalSensor> cached = new();

    public string Name => "ACPI Thermal Zones";

    public bool TryInitialise()
    {
        // Probe once: if the class returns nothing now, it will keep returning nothing.
        try {
            available = Wbem.Query(Namespace, Wql, "InstanceName").Count > 0;
        }
        catch {
            available = false;
        }

        return available;
    }

    public IEnumerable<ThermalSensor> Read()
    {
        if (!available) {
            return [];
        }

        if (DateTime.UtcNow - lastQueryUtc < CacheWindow) {
            return cached;
        }

        List<ThermalSensor> sensors = new();

        foreach (Dictionary<string, object?> row in
                 Wbem.Query(Namespace, Wql, "InstanceName", "CurrentTemperature", "CriticalTripPoint")) {

            string? instanceName = row.GetValueOrDefault("InstanceName") as string;

            if (AcpiThermalZone.TenthKelvinToCelsius(ToDouble(row.GetValueOrDefault("CurrentTemperature")))
                is not { } celsius) {

                continue;
            }

            sensors.Add(new ThermalSensor {
                Component = AcpiThermalZone.ClassifyZone(instanceName),
                ComponentId = string.Empty,
                SensorName = $"ACPI {AcpiThermalZone.ZoneLabel(instanceName)}",
                Celsius = celsius,
                CriticalCelsius = AcpiThermalZone.TenthKelvinToCelsius(
                    ToDouble(row.GetValueOrDefault("CriticalTripPoint"))),
                Source = ThermalSource.AcpiThermalZone
            });
        }

        cached = sensors;
        lastQueryUtc = DateTime.UtcNow;

        return sensors;
    }

    private static double ToDouble(object? value) => value switch {
        double d => d,
        float f => f,
        int i => i,
        uint u => u,
        long l => l,
        ulong ul => ul,
        short s => s,
        ushort us => us,
        string str when double.TryParse(str, out double parsed) => parsed,
        _ => 0
    };

    public void Dispose() { }
}
#endif
