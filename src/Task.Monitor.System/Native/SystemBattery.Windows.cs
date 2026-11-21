#if __WIN32__
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Native;

public static unsafe class SystemBattery
{
    public static double? GetDischargeWatts()
    {
        PowerBase.SYSTEM_BATTERY_STATE state = default;

        if (PowerBase.CallNtPowerInformation(
            PowerBase.SystemBatteryState,
            nint.Zero,
            0,
            &state,
            (uint)sizeof(PowerBase.SYSTEM_BATTERY_STATE)) != PowerBase.STATUS_SUCCESS) {

            return null;
        }

        if (state.BatteryPresent == 0 ||
            state.Discharging == 0 ||
            state.Rate is PowerBase.BATTERY_UNKNOWN_RATE or 0) {

            return null;
        }

        double watts = Math.Abs(state.Rate) / 1000.0;
        return watts is > 0 and < 1000 ? watts : null;
    }
}
#endif
