using System.Buffers.Binary;
using Task.Monitor.System.Services.Thermal;

namespace Task.Monitor.System.Services.Tests.Thermal;

public sealed class NvmeHealthLogTests
{
    private static byte[] Page(ushort compositeKelvin, params ushort[] sensorKelvin)
    {
        byte[] page = new byte[512];
        BinaryPrimitives.WriteUInt16LittleEndian(page.AsSpan(1), compositeKelvin);

        for (int i = 0; i < sensorKelvin.Length && i < 8; i++) {
            BinaryPrimitives.WriteUInt16LittleEndian(page.AsSpan(200 + i * 2), sensorKelvin[i]);
        }

        return page;
    }

    [Fact]
    public void Reads_The_Composite_Temperature()
    {
        // 313 K = 39.85 C
        double? celsius = NvmeHealthLog.CompositeCelsius(Page(313));

        Assert.NotNull(celsius);
        Assert.Equal(39.85, celsius!.Value, precision: 2);
    }

    [Fact]
    public void Treats_A_Zero_Composite_As_Absent()
    {
        Assert.Null(NvmeHealthLog.CompositeCelsius(Page(0)));
    }

    [Fact]
    public void Reads_Only_The_Implemented_Sensors()
    {
        IReadOnlyList<(int Index, double Celsius)> sensors =
            NvmeHealthLog.Sensors(Page(313, 315, 0, 350));

        // Slot 0 -> "Sensor 1", slot 1 is zero and skipped, slot 2 -> "Sensor 3".
        Assert.Equal(2, sensors.Count);
        Assert.Equal(1, sensors[0].Index);
        Assert.Equal(3, sensors[1].Index);
        Assert.Equal(315 - 273.15, sensors[0].Celsius, precision: 2);
    }

    [Fact]
    public void Rejects_An_Implausible_Reading()
    {
        Assert.Null(NvmeHealthLog.CompositeCelsius(Page(60000)));
    }

    [Fact]
    public void Returns_Nothing_For_A_Short_Page()
    {
        Assert.Null(NvmeHealthLog.CompositeCelsius(new byte[1]));
        Assert.Empty(NvmeHealthLog.Sensors(new byte[10]));
    }
}
