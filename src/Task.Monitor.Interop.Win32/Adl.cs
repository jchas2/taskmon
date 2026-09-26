using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;

namespace Task.Monitor.Interop.Win32;

public static unsafe class Adl
{
    // AMD display library, only the subset needed to read the GPU temp.
    private const string AtiAdlxx = "atiadlxx.dll";
    private const int AdlOk = 0;

    // AdapterInfo is a flat C struct; it is read by offset rather than marshalled.
    private const int AdapterInfoSize = 1572;
    private const int OffsetAdapterIndex = 4;       // iAdapterIndex
    private const int OffsetUdid = 8;               // strUDID[256]
    private const int OffsetVendorId = 276;         // iVendorID
    private const int OffsetPnpString = 1312;       // strPNPString[256]

    private const int Od6CurrentPowerTotal = 0;
    
    [DllImport(AtiAdlxx, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ADL2_Main_Control_Create(nint callback, int enumConnectedAdapters, out nint context);

    [DllImport(AtiAdlxx, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ADL2_Main_Control_Destroy(nint context);

    [DllImport(AtiAdlxx, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ADL2_Adapter_NumberOfAdapters_Get(nint context, out int count);

    [DllImport(AtiAdlxx, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ADL2_Adapter_AdapterInfo_Get(nint context, nint info, int inputSize);

    [DllImport(AtiAdlxx, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ADL2_Overdrive6_Temperature_Get(nint context, int adapterIndex, out int temperatureMilliC);

    [DllImport(AtiAdlxx, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ADL2_Overdrive5_Temperature_Get(
        nint context, 
        int adapterIndex, 
        int thermalControllerIndex, 
        out int temperatureMilliC);

    [DllImport(AtiAdlxx, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ADL2_Overdrive6_CurrentPower_Get(
        nint context, 
        int adapterIndex, 
        int powerType, 
        out int currentValueQ8);

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static nint Malloc(int size) => Marshal.AllocHGlobal(size);

    public readonly record struct Adapter(int AdapterIndex, uint VendorId, uint DeviceId);

    private const int AdlMaxPath = 256;

    public static bool TryCreate(out nint context)
    {
        context = nint.Zero;

        try {
            // The lib is only available when an AMD driver is installed.
            int status = ADL2_Main_Control_Create(
                (nint)(delegate* unmanaged[Stdcall]<int, nint>)&Malloc, 
                1, 
                out context);

            return status == AdlOk && context != nint.Zero;
        }
        catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException) {
            return false;
        }
    }

    public static void Destroy(nint context)
    {
        if (context == nint.Zero) {
            return;
        }

        try {
            ADL2_Main_Control_Destroy(context);
        }
        catch (DllNotFoundException) { }
    }

    public static IReadOnlyList<Adapter> GetAdapters(nint context)
    {
        if (ADL2_Adapter_NumberOfAdapters_Get(context, out int count) != AdlOk || count <= 0) {
            return [];
        }

        int bufferSize = AdapterInfoSize * count;
        nint buffer = Marshal.AllocHGlobal(bufferSize);

        new Span<byte>((void*)buffer, bufferSize).Clear();

        if (ADL2_Adapter_AdapterInfo_Get(context, buffer, bufferSize) != AdlOk) {
            Marshal.FreeHGlobal(buffer);
            return [];
        }

        List<Adapter> adapters = new();
        HashSet<int> adapterIndexes = new();

        for (int i = 0; i < count; i++) {
            byte* entry = (byte*)buffer + i * AdapterInfoSize;
            int adapterIndex = *(int*)(entry + OffsetAdapterIndex);

            // Several ADL "adapters" can map to one physical GPU; use the first.
            if (!adapterIndexes.Add(adapterIndex)) {
                continue;
            }

            string pnp = InteropHelper.TryMarshalPtrToStringAnsi(
                (nint)(entry + OffsetPnpString), AdlMaxPath) ?? string.Empty;
            
            string udid = InteropHelper.TryMarshalPtrToStringAnsi(
                (nint)(entry + OffsetUdid), AdlMaxPath) ?? string.Empty;

            uint vendorId = (uint)(*(int*)(entry + OffsetVendorId));
            
            _ = TryParseDeviceId(pnp.Length > 0 
                ? pnp 
                : udid, out uint deviceId);

            adapters.Add(new Adapter(adapterIndex, vendorId, deviceId));
        }

        Marshal.FreeHGlobal(buffer);
        return adapters;
    }

    public static bool GetTemperatureCelsius(nint context, int adapterIndex, out double celsius)
    {
        celsius = 0;

        if (ADL2_Overdrive6_Temperature_Get(context, adapterIndex, out int od6MilliC) == AdlOk && od6MilliC > 0) {
            celsius = od6MilliC / 1000.0;
            return celsius is > -40 and < 150;
        }

        if (ADL2_Overdrive5_Temperature_Get(context, adapterIndex, 0, out int od5MilliC) == AdlOk && od5MilliC > 0) {
            celsius = od5MilliC / 1000.0;
            return celsius is > -40 and < 150;
        }

        return false;
    }

    public static bool GetPowerWatts(nint context, int adapterIndex, out double watts)
    {
        watts = 0;

        if (ADL2_Overdrive6_CurrentPower_Get(
            context, 
            adapterIndex, 
            Od6CurrentPowerTotal, 
            out int q8) != AdlOk) {
            
            return false;
        }

        watts = q8 / 256.0;
        return watts is > 0 and < 2000;
    }

    private static bool TryParseDeviceId(string pnpOrUdid, out uint deviceId)
    {
        deviceId = 0;

        int index = pnpOrUdid.IndexOf("DEV_", StringComparison.OrdinalIgnoreCase);

        return index >= 0
            && index + 8 <= pnpOrUdid.Length
            && uint.TryParse(
                pnpOrUdid.AsSpan(index + 4, 4), 
                NumberStyles.HexNumber, 
                null, 
                out deviceId);
    }
}
