using System.Buffers.Binary;

namespace Task.Monitor.System.Services.Power;

// Reads the peak power (PSD0's Maximum Power) out of a 4096-byte
// NVMe Identify Controller structure. 
public static class NvmePowerState
{
    private const int Psd0Offset = 2048;

    public static double? PeakWatts(ReadOnlySpan<byte> identifyController)
    {
        if (identifyController.Length < Psd0Offset + 4) {
            return null;
        }

        ushort mp = BinaryPrimitives.ReadUInt16LittleEndian(identifyController[Psd0Offset..]);

        if (mp == 0) {
            return null;
        }

        bool maxPowerScale = (identifyController[Psd0Offset + 3] & 0x1) != 0;
        
        double watts = maxPowerScale 
            ? mp * 0.0001 
            : mp * 0.01;

        return watts is > 0 and < 100 ? watts : null;
    }
}
