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
        IntPtr key = CoreFoundation.CFStringCreate("gpu-core-count");

        IntPtr coreCountRef = IOKit.IORegistryEntrySearchCFProperty(
            service,
            IOServicePlane,
            key,
            IntPtr.Zero,
            RegistryIterateRecursively);

        CoreFoundation.CFRelease(key);

        if (coreCountRef != IntPtr.Zero) {
            CoreFoundation.CFNumberGetValue(coreCountRef, out long coreCount);
            cores = (int)coreCount;
            CoreFoundation.CFRelease(coreCountRef);
        }

        IOKit.IOObjectRelease(service);
        return cores;
    }
}
#endif
