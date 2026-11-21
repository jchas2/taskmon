#if __WIN32__
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Native;

public static unsafe class NvApiLibrary
{
    public readonly record struct ThermalReading(int Target, double Celsius);

    public static int EnumPhysicalGpus(Span<nint> handles)
    {
        nint function = NvApi.nvapi_QueryInterface(NvApi.OffsetEnumPhysicalGpus);

        if (function == nint.Zero || handles.Length < NvApi.NvApiMaxPhysicalGpus) {
            return 0;
        }

        int count = 0;

        fixed (nint* pHandles = handles) {
            if (NvApi.NvAPI_EnumPhysicalGPUs(function, pHandles, &count) != NvApi.NvApiOk) {
                return 0;
            }
        }

        return count;
    }

    public static IReadOnlyList<ThermalReading> GetThermalSettings(nint gpuHandle)
    {
        nint function = NvApi.nvapi_QueryInterface(NvApi.OffsetGpuGetThermalSettings);

        if (function == nint.Zero) {
            return [];
        }

        byte* buffer = stackalloc byte[NvApi.ThermalSettingsSize];
        new Span<byte>(buffer, NvApi.ThermalSettingsSize).Clear();
        *(uint*)buffer = NvApi.ThermalSettingsVersion;

        if (NvApi.NvAPI_GPU_GetThermalSettings(function, gpuHandle, NvApi.ThermalTargetAll, buffer) != NvApi.NvApiOk) {
            return [];
        }

        uint count = Math.Min(*(uint*)(buffer + 4), NvApi.ThermalSettingsMaxSensors);
        List<ThermalReading> readings = new((int)count);

        for (int i = 0; i < count; i++) {
            byte* sensor = buffer + 8 + i * NvApi.ThermalSensorStride;
            int current  = *(int*)(sensor + NvApi.ThermalSensorCurrentTempOffset);
            int target   = *(int*)(sensor + NvApi.ThermalSensorTargetOffset);

            if (current is > -40 and < 150) {
                readings.Add(new ThermalReading(target, current));
            }
        }

        return readings;
    }

    public static bool Initialize(out string diagnostic)
    {
        try {
            nint function = NvApi.nvapi_QueryInterface(NvApi.OffsetInitialize);

            if (function == nint.Zero) {
                diagnostic = $"FAILED, {NvApi.NvApiDll} has no NvAPI_Initialize";
                return false;
            }

            int status = NvApi.NvAPI_Initialize(function);

            diagnostic = status switch {
                NvApi.NvApiOk                   => "OK",
                NvApi.NvApiNvidiaDeviceNotFound => "NOT FOUND, no NVIDIA GPU",
                _                               => $"FAILED, NvAPI_Initialize returned NVAPI status {status}"
            };

            return status == NvApi.NvApiOk;
        }
        catch (DllNotFoundException) {
            diagnostic = $"NOT FOUND, {NvApi.NvApiDll} is not installed";
            return false;
        }
        catch (EntryPointNotFoundException) {
            diagnostic = $"FAILED, {NvApi.NvApiDll} has no nvapi_QueryInterface";
            return false;
        }
    }

    public static bool TryGetPciIds(nint gpuHandle, out uint vendorId, out uint deviceId)
    {
        vendorId = 0;
        deviceId = 0;

        nint function = NvApi.nvapi_QueryInterface(NvApi.OffsetGpuGetPciIdentifiers);

        if (function == nint.Zero) {
            return false;
        }

        uint pDeviceId    = 0;
        uint pSubSystemId = 0;
        uint pRevisionId  = 0;
        uint pExtDeviceId = 0;

        if (NvApi.NvAPI_GPU_GetPCIIdentifiers(
            function,
            gpuHandle,
            &pDeviceId,
            &pSubSystemId,
            &pRevisionId,
            &pExtDeviceId) != NvApi.NvApiOk) {

            return false;
        }

        // pDeviceId packs (internalDeviceId << 16) | vendorId.
        vendorId = pDeviceId & 0xFFFF;
        deviceId = (pDeviceId >> 16) & 0xFFFF;

        return true;
    }

    public static void Unload()
    {
        nint function = NvApi.nvapi_QueryInterface(NvApi.OffsetUnload);

        if (function != nint.Zero) {
            NvApi.NvAPI_Unload(function);
        }
    }
}
#endif
