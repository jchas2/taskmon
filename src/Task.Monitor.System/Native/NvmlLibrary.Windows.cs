#if __WIN32__
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Native;

public static unsafe class NvmlLibrary
{
    public static int DeviceCount() =>
        Nvml.nvmlDeviceGetCount_v2(out uint count) == Nvml.NvmlSuccess ? (int)count : 0;

    public static bool Initialize(out string diagnostic)
    {
        try {
            int status = Nvml.nvmlInit_v2();

            diagnostic = status == Nvml.NvmlSuccess
                ? "OK"
                : $"FAILED, {nameof(Nvml.nvmlInit_v2)} returned NVML status {status}";

            return status == Nvml.NvmlSuccess;
        }
        catch (DllNotFoundException) {
            diagnostic = $"NOT FOUND, {Nvml.NvmlDll} is not installed";
            return false;
        }
        catch (EntryPointNotFoundException) {
            diagnostic = $"FAILED, {Nvml.NvmlDll} has no {nameof(Nvml.nvmlInit_v2)}";
            return false;
        }
    }

    public static void Shutdown()
    {
        try {
            Nvml.nvmlShutdown();
        }
        catch (DllNotFoundException) { /* Driver dropped */ }
    }

    public static bool TryGetHandle(int index, out nint handle) =>
        Nvml.nvmlDeviceGetHandleByIndex_v2((uint)index, out handle) == Nvml.NvmlSuccess && handle != nint.Zero;

    public static bool TryGetPciIds(nint handle, out uint vendorId, out uint deviceId)
    {
        vendorId = 0;
        deviceId = 0;

        byte* buffer = stackalloc byte[Nvml.PciInfoSize];
        new Span<byte>(buffer, Nvml.PciInfoSize).Clear();

        if (Nvml.nvmlDeviceGetPciInfo_v3(handle, buffer) != Nvml.NvmlSuccess) {
            return false;
        }

        // pciDeviceId packs (deviceId << 16) | vendorId.
        uint pciDeviceId = *(uint*)(buffer + Nvml.PciInfoDeviceIdOffset);
        vendorId = pciDeviceId & 0xFFFF;
        deviceId = (pciDeviceId >> 16) & 0xFFFF;

        return true;
    }

    public static bool TryGetPowerWatts(nint handle, out double watts)
    {
        watts = 0;

        if (Nvml.nvmlDeviceGetPowerUsage(handle, out uint milliwatts) != Nvml.NvmlSuccess) {
            return false;
        }

        watts = milliwatts / 1000.0;
        return watts is >= 0 and < 2000;
    }
}
#endif
