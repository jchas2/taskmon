using System.Buffers.Binary;

namespace Task.Monitor.System.Services.Thermal;

// Reads temperatures out of a 512-byte NVMe SMART / Health Information log page (log page 0x02).
// Every temperature in the page is unsigned 16-bit Kelvin; 0 means the sensor is not implemented.
public static class NvmeHealthLog
{
    private const int CompositeTemperatureOffset = 1;
    private const int TemperatureSensorOffset = 200;
    private const int TemperatureSensorCount = 8;

    private const double KelvinToCelsius = 273.15;

    // The drive's headline reading. Null when the page is too short or the field is zero / implausible.
    public static double? CompositeCelsius(ReadOnlySpan<byte> healthLog)
    {
        if (healthLog.Length < CompositeTemperatureOffset + 2) {
            return null;
        }

        return ToCelsius(BinaryPrimitives.ReadUInt16LittleEndian(healthLog[CompositeTemperatureOffset..]));
    }

    // Sensors 1..8 in order, skipping any that read zero (not implemented) or implausible.
    public static IReadOnlyList<(int Index, double Celsius)> Sensors(ReadOnlySpan<byte> healthLog)
    {
        List<(int, double)> sensors = new();

        for (int sensor = 0; sensor < TemperatureSensorCount; sensor++) {
            int offset = TemperatureSensorOffset + sensor * 2;

            if (offset + 2 > healthLog.Length) {
                break;
            }

            ushort kelvin = BinaryPrimitives.ReadUInt16LittleEndian(healthLog[offset..]);

            if (ToCelsius(kelvin) is { } celsius) {
                sensors.Add((sensor + 1, celsius));
            }
        }

        return sensors;
    }

    private static double? ToCelsius(ushort kelvin)
    {
        if (kelvin == 0) {
            return null;
        }

        double celsius = kelvin - KelvinToCelsius;

        // A working drive sensor sits well inside this range; anything outside is a misread field.
        return celsius is > -40 and < 150 ? celsius : null;
    }
}
