namespace Task.Monitor.System.Services.Thermal;

// Reads temperature out of the 512-byte ATA SMART attribute table.
public static class SmartAttributeTable
{
    private const int FirstAttributeOffset = 2;
    private const int AttributeEntrySize = 12;
    private const int AttributeCount = 30;

    private const byte TemperatureCelsiusId = 194;  // 0xC2
    private const byte AirflowTemperatureId = 190;  // 0xBE

    public static double? TemperatureCelsius(ReadOnlySpan<byte> attributeTable)
    {
        if (Plausible(RawLowByte(attributeTable, TemperatureCelsiusId)) is { } primary) {
            return primary;
        }

        return Plausible(RawLowByte(attributeTable, AirflowTemperatureId));
    }

    private static double? Plausible(int? rawLowByte) =>
        rawLowByte is > 0 and < 120 
            ? rawLowByte 
            : null;

    private static int? RawLowByte(ReadOnlySpan<byte> attributeTable, byte attributeId)
    {
        for (int i = 0; i < AttributeCount; i++) {
            int offset = FirstAttributeOffset + i * AttributeEntrySize;

            if (offset + AttributeEntrySize > attributeTable.Length) {
                break;
            }

            if (attributeTable[offset] == attributeId) {
                return attributeTable[offset + 5]; // raw[0]
            }
        }

        return null;
    }
}
