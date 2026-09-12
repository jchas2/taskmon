using Task.Monitor.System.Services.Thermal;

namespace Task.Monitor.System.Services.Tests.Thermal;

public sealed class SmartAttributeTableTests
{
    // Builds a 512-byte SMART attribute table with one 12-byte entry per (id, rawLowByte) pair,
    // starting at offset 2.
    private static byte[] Table(params (byte Id, byte RawLowByte)[] attributes)
    {
        byte[] table = new byte[512];

        for (int i = 0; i < attributes.Length; i++) {
            int offset = 2 + i * 12;
            table[offset] = attributes[i].Id;
            table[offset + 5] = attributes[i].RawLowByte; // raw[0]
        }

        return table;
    }

    [Fact]
    public void Reads_Attribute_194()
    {
        Assert.Equal(38, SmartAttributeTable.TemperatureCelsius(Table((194, 38))));
    }

    [Fact]
    public void Falls_Back_To_Attribute_190()
    {
        Assert.Equal(41, SmartAttributeTable.TemperatureCelsius(Table((5, 0), (190, 41))));
    }

    [Fact]
    public void Prefers_194_Over_190()
    {
        Assert.Equal(38, SmartAttributeTable.TemperatureCelsius(Table((190, 41), (194, 38))));
    }

    [Fact]
    public void Returns_Null_When_No_Temperature_Attribute_Is_Present()
    {
        Assert.Null(SmartAttributeTable.TemperatureCelsius(Table((5, 0), (9, 100))));
    }

    [Fact]
    public void Rejects_An_Implausible_Value()
    {
        Assert.Null(SmartAttributeTable.TemperatureCelsius(Table((194, 200))));
    }
}
