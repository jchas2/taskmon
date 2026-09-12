namespace Task.Monitor.System.Services.Thermal;

// Helpers for the readings that come back from WMI's MSAcpi_ThermalZoneTemperature class.
public static class AcpiThermalZone
{
    // Temperatures in that class are reported in tenths of a Kelvin.
    public static double? TenthKelvinToCelsius(double tenthKelvin)
    {
        if (tenthKelvin <= 0) {
            return null;
        }

        double celsius = tenthKelvin / 10.0 - 273.15;

        return celsius is > -40 and < 150 ? celsius : null;
    }

    // ACPI zone instance names are cryptic ("ACPI\ThermalZone\TZ00_0", "\_TZ.CPUZ"). A few name the
    // CPU zone; everything else is treated as a generic chassis zone rather than guessed at.
    public static ThermalComponent ClassifyZone(string? instanceName)
    {
        if (string.IsNullOrEmpty(instanceName)) {
            return ThermalComponent.Other;
        }

        string upper = instanceName.ToUpperInvariant();

        return upper.Contains("CPU") || upper.Contains("PROC")
            ? ThermalComponent.Cpu
            : ThermalComponent.Other;
    }

    // A short, stable label from the zone's instance name.
    public static string ZoneLabel(string? instanceName)
    {
        if (string.IsNullOrEmpty(instanceName)) {
            return "Thermal Zone";
        }

        int lastSeparator = instanceName.LastIndexOfAny(['\\', '.', '/']);

        string tail = lastSeparator >= 0 && lastSeparator < instanceName.Length - 1
            ? instanceName[(lastSeparator + 1)..]
            : instanceName;

        return tail.Length > 0 ? tail : "Thermal Zone";
    }
}
