using System.Buffers.Binary;
using Task.Monitor.System.Services.Power;

namespace Task.Monitor.System.Services.Tests.Power;

public sealed class NvmePowerStateTests
{
    private static byte[] Identify(ushort maxPower, bool maxPowerScale)
    {
        byte[] data = new byte[4096];
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(2048), maxPower);

        if (maxPowerScale) {
            data[2048 + 3] |= 0x1;
        }

        return data;
    }

    [Fact]
    public void Reads_Centiwatt_Scaled_Max_Power()
    {
        // MXPS clear => units of 0.01 W. 550 => 5.5 W.
        Assert.Equal(5.5, NvmePowerState.PeakWatts(Identify(550, maxPowerScale: false))!.Value, precision: 2);
    }

    [Fact]
    public void Reads_Microwatt_Scaled_Max_Power()
    {
        // MXPS set => units of 0.0001 W. 12000 => 1.2 W.
        Assert.Equal(1.2, NvmePowerState.PeakWatts(Identify(12000, maxPowerScale: true))!.Value, precision: 3);
    }

    [Fact]
    public void Returns_Null_For_A_Zero_Or_Short_Structure()
    {
        Assert.Null(NvmePowerState.PeakWatts(Identify(0, maxPowerScale: false)));
        Assert.Null(NvmePowerState.PeakWatts(new byte[100]));
    }

    [Fact]
    public void Rejects_An_Implausible_Value()
    {
        Assert.Null(NvmePowerState.PeakWatts(Identify(20000, maxPowerScale: false))); // 200 W
    }
}
