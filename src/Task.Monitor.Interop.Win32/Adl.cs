using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

// AMD display library, only the subset needed to read the GPU temperature and power.
public static class Adl
{
    public const string AtiAdlxx = "atiadlxx.dll";

    public const int AdlOk = 0;

    // AdapterInfo is a flat C struct; it is read by offset rather than marshalled.
    public const int AdapterInfoSize    = 1572;
    public const int OffsetAdapterIndex = 4;       // iAdapterIndex
    public const int OffsetUdid         = 8;       // strUDID[256]
    public const int OffsetVendorId     = 276;     // iVendorID
    public const int OffsetPnpString    = 1312;    // strPNPString[256]

    public const int AdlMaxPath = 256;

    public const int Od6CurrentPowerTotal = 0;

    [DllImport(AtiAdlxx, CallingConvention = CallingConvention.Cdecl)]
    public static extern int ADL2_Adapter_AdapterInfo_Get(nint context, nint info, int inputSize);

    [DllImport(AtiAdlxx, CallingConvention = CallingConvention.Cdecl)]
    public static extern int ADL2_Adapter_NumberOfAdapters_Get(nint context, out int count);

    [DllImport(AtiAdlxx, CallingConvention = CallingConvention.Cdecl)]
    public static extern int ADL2_Main_Control_Create(nint callback, int enumConnectedAdapters, out nint context);

    [DllImport(AtiAdlxx, CallingConvention = CallingConvention.Cdecl)]
    public static extern int ADL2_Main_Control_Destroy(nint context);

    [DllImport(AtiAdlxx, CallingConvention = CallingConvention.Cdecl)]
    public static extern int ADL2_Overdrive5_Temperature_Get(
        nint context,
        int adapterIndex,
        int thermalControllerIndex,
        out int temperatureMilliC);

    [DllImport(AtiAdlxx, CallingConvention = CallingConvention.Cdecl)]
    public static extern int ADL2_Overdrive6_CurrentPower_Get(
        nint context,
        int adapterIndex,
        int powerType,
        out int currentValueQ8);

    [DllImport(AtiAdlxx, CallingConvention = CallingConvention.Cdecl)]
    public static extern int ADL2_Overdrive6_Temperature_Get(nint context, int adapterIndex, out int temperatureMilliC);
}
