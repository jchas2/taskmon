using System.Runtime.InteropServices;

namespace Task.Monitor.Interop.Win32;

// NVIDIA Management Library, declaring the functions needed to read a GPU's power draw.
public static unsafe class Nvml
{
    public const string NvmlDll = "nvml.dll";

    public const int NvmlSuccess           = 0;
    public const int PciInfoSize           = 64;
    public const int PciInfoDeviceIdOffset = 28;

    [DllImport(NvmlDll, EntryPoint = "nvmlDeviceGetCount_v2")]
    public static extern int nvmlDeviceGetCount_v2(out uint deviceCount);

    [DllImport(NvmlDll, EntryPoint = "nvmlDeviceGetEnforcedPowerLimit")]
    public static extern int nvmlDeviceGetEnforcedPowerLimit(nint device, out uint milliwatts);

    [DllImport(NvmlDll, EntryPoint = "nvmlDeviceGetHandleByIndex_v2")]
    public static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out nint device);

    [DllImport(NvmlDll, EntryPoint = "nvmlDeviceGetPciInfo_v3")]
    public static extern int nvmlDeviceGetPciInfo_v3(nint device, byte* pciInfo);

    [DllImport(NvmlDll, EntryPoint = "nvmlDeviceGetPowerUsage")]
    public static extern int nvmlDeviceGetPowerUsage(nint device, out uint milliwatts);

    [DllImport(NvmlDll, EntryPoint = "nvmlInit_v2")]
    public static extern int nvmlInit_v2();

    [DllImport(NvmlDll, EntryPoint = "nvmlShutdown")]
    public static extern int nvmlShutdown();
}
