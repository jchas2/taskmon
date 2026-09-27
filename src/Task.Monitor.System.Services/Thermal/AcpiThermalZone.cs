namespace Task.Monitor.System.Services.Thermal;

public static class AcpiThermalZone
{
    public static double? TenthKelvinToCelsius(double tenthKelvin)
    {
        // Temperatures in that class are reported in tenths of a Kelvin.
        if (tenthKelvin <= 0) {
            return null;
        }

        double celsius = tenthKelvin / 10.0 - 273.15;
        return celsius is > -40 and < 150 ? celsius : null;
    }

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
