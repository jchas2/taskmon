using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

// powrprof.dll - just the system battery state, which carries the whole-machine charge/discharge
// rate in milliwatts. On AC power (or a desktop) the rate is not meaningful.
public static unsafe class PowerBase
{
    // POWER_INFORMATION_LEVEL.SystemBatteryState
    private const int SystemBatteryState = 5;

    private const int STATUS_SUCCESS = 0;

    // Rate returns this when the driver cannot report it.
    private const int BatteryUnknownRate = unchecked((int)0x80000000);

    // SYSTEM_BATTERY_STATE, 40 bytes:
    //   +0  AcOnLine (BOOLEAN)   +1 BatteryPresent   +2 Charging   +3 Discharging
    //   +4  Spare1[3]            +7 Tag
    //   +8  MaxCapacity (ULONG)  +12 RemainingCapacity   +16 Rate (LONG, signed; < 0 = discharging)
    //   +20 EstimatedTime        +24 DefaultAlert1        +28 DefaultAlert2
    private const int SystemBatteryStateSize = 40;
    private const int OffsetBatteryPresent = 1;
    private const int OffsetDischarging = 3;
    private const int OffsetRate = 16;

    [DllImport(Libraries.PowrProf)]
    private static extern int CallNtPowerInformation(
        int informationLevel, nint inputBuffer, uint inputBufferLength, byte* outputBuffer, uint outputBufferLength);

    /// <summary>
    /// The whole-system power draw in watts while running on battery, or null on AC / no battery /
    /// a driver that will not report the rate.
    /// </summary>
    public static double? SystemDischargeWatts()
    {
        byte* buffer = stackalloc byte[SystemBatteryStateSize];
        new Span<byte>(buffer, SystemBatteryStateSize).Clear();

        if (CallNtPowerInformation(SystemBatteryState, nint.Zero, 0, buffer, SystemBatteryStateSize) != STATUS_SUCCESS) {
            return null;
        }

        bool present = buffer[OffsetBatteryPresent] != 0;
        bool discharging = buffer[OffsetDischarging] != 0;
        int rate = *(int*)(buffer + OffsetRate);

        if (!present || !discharging || rate == BatteryUnknownRate || rate == 0) {
            return null;
        }

        double watts = Math.Abs(rate) / 1000.0;
        return watts is > 0 and < 1000 ? watts : null;
    }
}
