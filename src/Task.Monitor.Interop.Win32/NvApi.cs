using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

// NVIDIA NVAPI, bound through nvapi_QueryInterface function-pointer dispatch.
public static unsafe class NvApi
{
    public const string NvApiDll = "nvapi64.dll";

    // nvapi_QueryInterface offsets.
    public const uint OffsetInitialize              = 0x0150E828;
    public const uint OffsetUnload                  = 0xD22BDD7E;
    public const uint OffsetEnumPhysicalGpus        = 0xE5AC921F;
    public const uint OffsetGpuGetThermalSettings   = 0xE3640A56;
    public const uint OffsetGpuGetPciIdentifiers    = 0x2DDFB66E;

    public const int NvApiOk                   = 0;
    public const int NvApiNvidiaDeviceNotFound = -6;

    public const int NvApiMaxPhysicalGpus = 64;

    public const uint ThermalTargetAll = 15;

    // NV_GPU_THERMAL_SETTINGS_V2: version(4) + count(4) + sensor[3] of 5 x int32.
    public const int  ThermalSettingsSize            = 8 + 3 * 20;
    public const uint ThermalSettingsVersion         = ThermalSettingsSize | (2u << 16);
    public const uint ThermalSettingsMaxSensors      = 3;
    public const int  ThermalSensorStride            = 20;
    public const int  ThermalSensorCurrentTempOffset = 12;
    public const int  ThermalSensorTargetOffset      = 16;

    // NV_THERMAL_TARGET.
    public const int TargetGpu         = 1;
    public const int TargetMemory      = 2;
    public const int TargetPowerSupply = 4;
    public const int TargetBoard       = 8;

    public static int NvAPI_EnumPhysicalGPUs(nint function, nint* handles, int* count) =>
        ((delegate* unmanaged[Cdecl]<nint*, int*, int>)function)(handles, count);

    public static int NvAPI_GPU_GetPCIIdentifiers(
        nint  function,
        nint  gpu,
        uint* deviceId,
        uint* subSystemId,
        uint* revisionId,
        uint* extDeviceId) =>
        ((delegate* unmanaged[Cdecl]<nint, uint*, uint*, uint*, uint*, int>)function)(
            gpu, deviceId, subSystemId, revisionId, extDeviceId);

    public static int NvAPI_GPU_GetThermalSettings(nint function, nint gpu, uint sensorIndex, void* settings) =>
        ((delegate* unmanaged[Cdecl]<nint, uint, void*, int>)function)(gpu, sensorIndex, settings);

    public static int NvAPI_Initialize(nint function) =>
        ((delegate* unmanaged[Cdecl]<int>)function)();

    [DllImport(NvApiDll, EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
    public static extern nint nvapi_QueryInterface(uint offset);

    public static int NvAPI_Unload(nint function) =>
        ((delegate* unmanaged[Cdecl]<int>)function)();
}
