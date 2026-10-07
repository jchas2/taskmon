#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Gpu;

public partial class GpuService
{
    private const string AppleVendor = "Apple";
    private const string AppleGpuDescription = "Apple GPU";
    private const string IntegratedAdapterType = "Integrated";

    private void OnStartGpuSpecs(GpuSpecs specs)
    {
        long totalMemory = TryReadIOAcceleratorMemory(out long allocMemory, out _)
            ? allocMemory
            : 0;

        specs.GpuCores = GetGpuCoreCount();
        specs.TotalGpuMemory = totalMemory;

        specs.Devices.Clear();
        specs.Devices.Add(new GpuDevice {
            Index                = 0,
            Vendor               = AppleVendor,
            // A precise model string ("Apple M1 Pro") would require Metal (MTLDevice.name); the
            // IORegistry "model" property is typed inconsistently across SoCs, so a safe label is used.
            Description          = AppleGpuDescription,
            AdapterType          = IntegratedAdapterType,
            DedicatedVideoMemory = 0,
            SharedSystemMemory   = totalMemory
        });
    }

    private static int GetGpuCoreCount()
    {
        IntPtr matching = IOKit.IOServiceMatching("AGXAccelerator");
        uint service = IOKit.IOServiceGetMatchingService(0, matching);

        if (service == 0) {
            return 0;
        }

        int cores = 0;
        using CFScope key = new(CoreFoundation.CFStringCreate("gpu-core-count"));

        using CFScope coreCount = new(IOKit.IORegistryEntrySearchCFProperty(
            service,
            IOServicePlane,
            key,
            IntPtr.Zero,
            RegistryIterateRecursively));

        if (!coreCount.IsNull) {
            CoreFoundation.CFNumberGetValue(coreCount, out long value);
            cores = (int)value;
        }

        IOKit.IOObjectRelease(service);
        return cores;
    }
}
#endif
