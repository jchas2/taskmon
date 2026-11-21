using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

public static unsafe class PowerBase
{
    // POWER_INFORMATION_LEVEL.SystemBatteryState
    public const int SystemBatteryState = 5;

    public const int STATUS_SUCCESS = 0;

    public const int BATTERY_UNKNOWN_RATE = unchecked((int)0x80000000);

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_BATTERY_STATE
    {
        public byte       AcOnLine;
        public byte       BatteryPresent;
        public byte       Charging;
        public byte       Discharging;
        public fixed byte Spare1[3];
        public byte       Tag;
        public uint       MaxCapacity;
        public uint       RemainingCapacity;
        public int        Rate;
        public uint       EstimatedTime;
        public uint       DefaultAlert1;
        public uint       DefaultAlert2;
    }

    [DllImport(Libraries.PowrProf)]
    public static extern int CallNtPowerInformation(
        int   informationLevel,
        nint  inputBuffer,
        uint  inputBufferLength,
        void* outputBuffer,
        uint  outputBufferLength);
}
