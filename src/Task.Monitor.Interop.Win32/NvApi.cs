using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

/// <summary>
/// NVIDIA NVAPI, bound through its <c>nvapi_QueryInterface</c> function-pointer dispatch rather than
/// the (never shipped) import library. Same raw-pointer approach as <see cref="Dxgi"/> for AOT
/// safety: every entry point is a plain function pointer and the structs are plain buffers.
///
/// Only what the thermal provider needs: initialise, enumerate GPUs, read thermal settings, and
/// read PCI ids so a handle can be matched back to a DXGI adapter.
/// </summary>
public static unsafe class NvApi
{
    private const string Nvapi = "nvapi64.dll";

    // nvapi_QueryInterface offsets (64-bit, stable across driver releases).
    private const uint OffsetInitialize             = 0x0150E828;
    private const uint OffsetUnload                  = 0xD22BDD7E;
    private const uint OffsetEnumPhysicalGpus        = 0xE5AC921F;
    private const uint OffsetGpuGetThermalSettings   = 0xE3640A56;
    private const uint OffsetGpuGetPciIdentifiers    = 0x2DDFB66E;

    public const int NvApiMaxPhysicalGpus = 64;

    private const uint ThermalTargetAll = 15;

    // NV_GPU_THERMAL_SETTINGS_V2: version(4) + count(4) + sensor[3] of 5 x int32 (20 bytes each).
    private const int ThermalSettingsSize = 8 + 3 * 20;
    private const uint ThermalSettingsVersion = ThermalSettingsSize | (2u << 16);
    private const int ThermalSensorStride = 20;
    private const int ThermalSensorCurrentTempOffset = 12;
    private const int ThermalSensorTargetOffset = 16;

    // NV_THERMAL_TARGET
    public const int TargetGpu = 1;
    public const int TargetMemory = 2;
    public const int TargetPowerSupply = 4;
    public const int TargetBoard = 8;

    [DllImport(Nvapi, EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint nvapi_QueryInterface(uint offset);

    public static bool Initialize()
    {
        nint fn = nvapi_QueryInterface(OffsetInitialize);
        return fn != nint.Zero && ((delegate* unmanaged[Cdecl]<int>)fn)() == 0;
    }

    public static void Unload()
    {
        nint fn = nvapi_QueryInterface(OffsetUnload);

        if (fn != nint.Zero) {
            ((delegate* unmanaged[Cdecl]<int>)fn)();
        }
    }

    public static int EnumPhysicalGpus(Span<nint> handles)
    {
        nint fn = nvapi_QueryInterface(OffsetEnumPhysicalGpus);

        if (fn == nint.Zero || handles.Length < NvApiMaxPhysicalGpus) {
            return 0;
        }

        int count = 0;

        fixed (nint* pHandles = handles) {
            if (((delegate* unmanaged[Cdecl]<nint*, int*, int>)fn)(pHandles, &count) != 0) {
                return 0;
            }
        }

        return count;
    }

    public readonly record struct ThermalReading(int Target, double Celsius);

    public static IReadOnlyList<ThermalReading> GetThermalSettings(nint gpuHandle)
    {
        nint fn = nvapi_QueryInterface(OffsetGpuGetThermalSettings);

        if (fn == nint.Zero) {
            return [];
        }

        byte* buffer = stackalloc byte[ThermalSettingsSize];
        new Span<byte>(buffer, ThermalSettingsSize).Clear();
        *(uint*)buffer = ThermalSettingsVersion;

        int status = ((delegate* unmanaged[Cdecl]<nint, uint, void*, int>)fn)(
            gpuHandle, ThermalTargetAll, buffer);

        if (status != 0) {
            return [];
        }

        uint count = Math.Min(*(uint*)(buffer + 4), 3u);
        List<ThermalReading> readings = new((int)count);

        for (int i = 0; i < count; i++) {
            byte* sensor = buffer + 8 + i * ThermalSensorStride;
            int current = *(int*)(sensor + ThermalSensorCurrentTempOffset);
            int target = *(int*)(sensor + ThermalSensorTargetOffset);

            if (current is > -40 and < 150) {
                readings.Add(new ThermalReading(target, current));
            }
        }

        return readings;
    }

    // pDeviceId packs (internalDeviceId << 16) | vendorId.
    public static bool TryGetPciIds(nint gpuHandle, out uint vendorId, out uint deviceId)
    {
        vendorId = 0;
        deviceId = 0;

        nint fn = nvapi_QueryInterface(OffsetGpuGetPciIdentifiers);

        if (fn == nint.Zero) {
            return false;
        }

        uint pDeviceId = 0;
        uint pSubSystemId = 0;
        uint pRevisionId = 0;
        uint pExtDeviceId = 0;

        int status = ((delegate* unmanaged[Cdecl]<nint, uint*, uint*, uint*, uint*, int>)fn)(
            gpuHandle, &pDeviceId, &pSubSystemId, &pRevisionId, &pExtDeviceId);

        if (status != 0) {
            return false;
        }

        vendorId = pDeviceId & 0xFFFF;
        deviceId = (pDeviceId >> 16) & 0xFFFF;

        return true;
    }
}
