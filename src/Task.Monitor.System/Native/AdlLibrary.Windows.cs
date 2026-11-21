#if __WIN32__
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Native;

public static unsafe class AdlLibrary
{
    public readonly record struct Adapter(int AdapterIndex, uint VendorId, uint DeviceId);

    public static void Destroy(nint context)
    {
        if (context == nint.Zero) {
            return;
        }

        try {
            Adl.ADL2_Main_Control_Destroy(context);
        }
        catch (DllNotFoundException) { }
    }

    public static IReadOnlyList<Adapter> GetAdapters(nint context)
    {
        if (Adl.ADL2_Adapter_NumberOfAdapters_Get(context, out int count) != Adl.AdlOk || count <= 0) {
            return [];
        }

        int bufferSize = Adl.AdapterInfoSize * count;

        using HGlobalScope buffer = HGlobalScope.Allocate(bufferSize);

        new Span<byte>((void*)buffer.Value, bufferSize).Clear();

        if (Adl.ADL2_Adapter_AdapterInfo_Get(context, buffer, bufferSize) != Adl.AdlOk) {
            return [];
        }

        List<Adapter> adapters = new();
        HashSet<int> adapterIndexes = new();

        for (int i = 0; i < count; i++) {
            byte* entry = (byte*)buffer.Value + i * Adl.AdapterInfoSize;
            int adapterIndex = *(int*)(entry + Adl.OffsetAdapterIndex);

            // Several ADL "adapters" can map to one physical GPU; use the first.
            if (!adapterIndexes.Add(adapterIndex)) {
                continue;
            }

            string pnp = InteropHelper.TryMarshalPtrToStringAnsi(
                (nint)(entry + Adl.OffsetPnpString), Adl.AdlMaxPath) ?? string.Empty;

            string udid = InteropHelper.TryMarshalPtrToStringAnsi(
                (nint)(entry + Adl.OffsetUdid), Adl.AdlMaxPath) ?? string.Empty;

            uint vendorId = (uint)(*(int*)(entry + Adl.OffsetVendorId));

            _ = TryParseDeviceId(pnp.Length > 0
                ? pnp
                : udid, out uint deviceId);

            adapters.Add(new Adapter(adapterIndex, vendorId, deviceId));
        }

        return adapters;
    }

    public static bool GetPowerWatts(nint context, int adapterIndex, out double watts)
    {
        watts = 0;

        if (Adl.ADL2_Overdrive6_CurrentPower_Get(
            context,
            adapterIndex,
            Adl.Od6CurrentPowerTotal,
            out int q8) != Adl.AdlOk) {

            return false;
        }

        watts = q8 / 256.0;
        return watts is > 0 and < 2000;
    }

    public static bool GetTemperatureCelsius(nint context, int adapterIndex, out double celsius)
    {
        celsius = 0;

        if (Adl.ADL2_Overdrive6_Temperature_Get(context, adapterIndex, out int od6MilliC) == Adl.AdlOk && od6MilliC > 0) {
            celsius = od6MilliC / 1000.0;
            return celsius is > -40 and < 150;
        }

        if (Adl.ADL2_Overdrive5_Temperature_Get(context, adapterIndex, 0, out int od5MilliC) == Adl.AdlOk && od5MilliC > 0) {
            celsius = od5MilliC / 1000.0;
            return celsius is > -40 and < 150;
        }

        return false;
    }

    // ADL allocates the memory it returns through this callback.
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static nint Malloc(int size) => Marshal.AllocHGlobal(size);

    public static bool TryCreate(out nint context, out string diagnostic)
    {
        context = nint.Zero;

        try {
            int status = Adl.ADL2_Main_Control_Create(
                (nint)(delegate* unmanaged[Stdcall]<int, nint>)&Malloc,
                1,
                out context);

            bool created = status == Adl.AdlOk && context != nint.Zero;

            diagnostic = created
                ? "OK"
                : $"FAILED, ADL2_Main_Control_Create returned ADL status {status}";

            return created;
        }
        catch (DllNotFoundException) {
            diagnostic = $"NOT FOUND, {Adl.AtiAdlxx} is not installed";
            return false;
        }
        catch (EntryPointNotFoundException) {
            diagnostic = $"FAILED, {Adl.AtiAdlxx} has no ADL2_Main_Control_Create";
            return false;
        }
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
#endif
