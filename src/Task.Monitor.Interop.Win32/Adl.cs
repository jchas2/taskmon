using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

/// <summary>
/// AMD Display Library (atiadlxx.dll), the subset needed to read a GPU's temperature.
///
/// Plain <c>[DllImport]</c> here rather than a vtable - ADL is a flat C API, not COM. The library
/// is only present when an AMD driver is installed, so every entry is reached through
/// <see cref="TryCreate"/> which swallows <see cref="DllNotFoundException"/>.
///
/// Covers the Overdrive 6 and Overdrive 5 temperature calls, which between them handle GCN and
/// earlier. RDNA's PMLog path is a follow-up.
/// </summary>
public static unsafe class Adl
{
    private const string AtiAdlxx = "atiadlxx.dll";

    private const int AdlOk = 0;

    // AdapterInfo is a flat C struct; it is read by offset rather than marshalled.
    //   +0   iSize            +4   iAdapterIndex     +8   strUDID[256]
    //   +276 iVendorID        +1312 strPNPString[256]
    private const int AdapterInfoSize = 1572;
    private const int OffsetAdapterIndex = 4;
    private const int OffsetUdid = 8;
    private const int OffsetVendorId = 276;
    private const int OffsetPnpString = 1312;

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
        nint context, int adapterIndex, int thermalControllerIndex, out int temperatureMilliC);

    [DllImport(AtiAdlxx, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ADL2_Overdrive6_CurrentPower_Get(
        nint context, int adapterIndex, int powerType, out int currentValueQ8);

    // ADL_OD6_CURRENTPOWER_TOTAL
    private const int Od6CurrentPowerTotal = 0;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static nint Malloc(int size) => Marshal.AllocHGlobal(size);

    public readonly record struct Adapter(int AdapterIndex, uint VendorId, uint DeviceId);

    public static bool TryCreate(out nint context)
    {
        context = nint.Zero;

        try {
            int status = ADL2_Main_Control_Create(
                (nint)(delegate* unmanaged[Stdcall]<int, nint>)&Malloc, 1, out context);

            return status == AdlOk && context != nint.Zero;
        }
        catch (DllNotFoundException) {
            return false;
        }
        catch (EntryPointNotFoundException) {
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
        catch (DllNotFoundException) {
            // The library vanished under us - nothing to clean up.
        }
    }

    public static IReadOnlyList<Adapter> GetAdapters(nint context)
    {
        if (ADL2_Adapter_NumberOfAdapters_Get(context, out int count) != AdlOk || count <= 0) {
            return [];
        }

        int bufferSize = AdapterInfoSize * count;
        nint buffer = Marshal.AllocHGlobal(bufferSize);

        try {
            new Span<byte>((void*)buffer, bufferSize).Clear();

            if (ADL2_Adapter_AdapterInfo_Get(context, buffer, bufferSize) != AdlOk) {
                return [];
            }

            List<Adapter> adapters = new();
            HashSet<int> seen = new();

            for (int i = 0; i < count; i++) {
                byte* entry = (byte*)buffer + i * AdapterInfoSize;
                int adapterIndex = *(int*)(entry + OffsetAdapterIndex);

                // Several ADL "adapters" map to one physical GPU (one per output); keep the first.
                if (!seen.Add(adapterIndex)) {
                    continue;
                }

                string pnp = Marshal.PtrToStringAnsi((nint)(entry + OffsetPnpString)) ?? string.Empty;
                string udid = Marshal.PtrToStringAnsi((nint)(entry + OffsetUdid)) ?? string.Empty;

                uint vendorId = (uint)(*(int*)(entry + OffsetVendorId));
                TryParseDeviceId(pnp.Length > 0 ? pnp : udid, out uint deviceId);

                adapters.Add(new Adapter(adapterIndex, vendorId, deviceId));
            }

            return adapters;
        }
        finally {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public static bool TryGetTemperatureCelsius(nint context, int adapterIndex, out double celsius)
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

    // Overdrive 6 total board power (GCN / Polaris / Vega). RDNA's PMLog path is a follow-up, so a
    // newer card simply reports no power here.
    public static bool TryGetPowerWatts(nint context, int adapterIndex, out double watts)
    {
        watts = 0;

        if (ADL2_Overdrive6_CurrentPower_Get(context, adapterIndex, Od6CurrentPowerTotal, out int q8) != AdlOk) {
            return false;
        }

        // Q8.8 fixed point.
        watts = q8 / 256.0;
        return watts is > 0 and < 2000;
    }

    private static bool TryParseDeviceId(string pnpOrUdid, out uint deviceId)
    {
        deviceId = 0;

        int index = pnpOrUdid.IndexOf("DEV_", StringComparison.OrdinalIgnoreCase);

        return index >= 0
            && index + 8 <= pnpOrUdid.Length
            && uint.TryParse(pnpOrUdid.AsSpan(index + 4, 4), NumberStyles.HexNumber, null, out deviceId);
    }
}
