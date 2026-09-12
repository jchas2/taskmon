using System.Buffers.Binary;

namespace Task.Monitor.System.Services.Power;

// Reads the peak power (PSD0's Maximum Power) out of a 4096-byte NVMe Identify Controller
// structure. Power State Descriptors start at offset 2048, 32 bytes each; PSD0 is the
// max-performance state, so its MP is the drive's nameplate peak.
//   +0..1 MP   +3 bit0 MXPS  (0 => MP in units of 0.01 W, 1 => units of 0.0001 W)
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
        double watts = maxPowerScale ? mp * 0.0001 : mp * 0.01;

        return watts is > 0 and < 100 ? watts : null;
    }
}
