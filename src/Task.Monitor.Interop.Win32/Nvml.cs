using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

/// <summary>
/// NVIDIA Management Library (nvml.dll), the subset needed to read a GPU's power draw. NVML ships
/// with every NVIDIA driver and is a flat, documented C API, so plain <c>[DllImport]</c> is fine -
/// no vtable. Absent when there is no NVIDIA driver, so every entry is reached through
/// <see cref="Initialize"/>, which swallows <see cref="DllNotFoundException"/>.
///
/// Power comes back in milliwatts (instantaneous) and energy in millijoules (cumulative); the
/// caller can use either.
/// </summary>
public static unsafe class Nvml
{
    private const string NvmlDll = "nvml.dll";

    private const int NvmlSuccess = 0;

    // nvmlPciInfo_t: char busIdLegacy[16], uint domain, uint bus, uint device, uint pciDeviceId, ...
    private const int PciInfoSize = 64;
    private const int PciInfoDeviceIdOffset = 28;

    [DllImport(NvmlDll, EntryPoint = "nvmlInit_v2")]
    private static extern int nvmlInit_v2();

    [DllImport(NvmlDll, EntryPoint = "nvmlShutdown")]
    private static extern int nvmlShutdown();

    [DllImport(NvmlDll, EntryPoint = "nvmlDeviceGetCount_v2")]
    private static extern int nvmlDeviceGetCount_v2(out uint deviceCount);

    [DllImport(NvmlDll, EntryPoint = "nvmlDeviceGetHandleByIndex_v2")]
    private static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out nint device);

    [DllImport(NvmlDll, EntryPoint = "nvmlDeviceGetPowerUsage")]
    private static extern int nvmlDeviceGetPowerUsage(nint device, out uint milliwatts);

    [DllImport(NvmlDll, EntryPoint = "nvmlDeviceGetEnforcedPowerLimit")]
    private static extern int nvmlDeviceGetEnforcedPowerLimit(nint device, out uint milliwatts);

    [DllImport(NvmlDll, EntryPoint = "nvmlDeviceGetPciInfo_v3")]
    private static extern int nvmlDeviceGetPciInfo_v3(nint device, byte* pciInfo);

    public static bool Initialize()
    {
        try {
            return nvmlInit_v2() == NvmlSuccess;
        }
        catch (DllNotFoundException) {
            return false;
        }
        catch (EntryPointNotFoundException) {
            return false;
        }
    }

    public static void Shutdown()
    {
        try {
            nvmlShutdown();
        }
        catch (DllNotFoundException) {
            // Driver went away - nothing to shut down.
        }
    }

    public static int DeviceCount() =>
        nvmlDeviceGetCount_v2(out uint count) == NvmlSuccess ? (int)count : 0;

    public static bool TryGetHandle(int index, out nint handle) =>
        nvmlDeviceGetHandleByIndex_v2((uint)index, out handle) == NvmlSuccess && handle != nint.Zero;

    public static bool TryGetPowerWatts(nint handle, out double watts)
    {
        watts = 0;

        if (nvmlDeviceGetPowerUsage(handle, out uint milliwatts) != NvmlSuccess) {
            return false;
        }

        watts = milliwatts / 1000.0;
        return watts is >= 0 and < 2000;
    }

    public static double? PowerLimitWatts(nint handle) =>
        nvmlDeviceGetEnforcedPowerLimit(handle, out uint milliwatts) == NvmlSuccess
            ? milliwatts / 1000.0
            : null;

    // pciDeviceId packs (deviceId << 16) | vendorId.
    public static bool TryGetPciIds(nint handle, out uint vendorId, out uint deviceId)
    {
        vendorId = 0;
        deviceId = 0;

        byte* buffer = stackalloc byte[PciInfoSize];
        new Span<byte>(buffer, PciInfoSize).Clear();

        if (nvmlDeviceGetPciInfo_v3(handle, buffer) != NvmlSuccess) {
            return false;
        }

        uint pciDeviceId = *(uint*)(buffer + PciInfoDeviceIdOffset);
        vendorId = pciDeviceId & 0xFFFF;
        deviceId = (pciDeviceId >> 16) & 0xFFFF;

        return true;
    }
}
