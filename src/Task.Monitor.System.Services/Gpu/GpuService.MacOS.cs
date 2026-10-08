#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Gpu;

public sealed partial class GpuService
{
    private const string IOServicePlane = "IOService";
    private const uint RegistryIterateRecursively = 0x00000001;

    private static bool TryReadIOAcceleratorMemory(out long allocMemory, out long inUseMemory)
    {
        allocMemory = 0;
        inUseMemory = 0;

        IntPtr matching = IOKit.IOServiceMatching("IOAccelerator");
        using IOObjectScope accelerator = new(IOKit.IOServiceGetMatchingService(0, matching));

        if (accelerator.IsNull) {
            return false;
        }

        int result = IOKit.IORegistryEntryCreateCFProperties(
            accelerator,
            out IntPtr propertiesRef,
            IntPtr.Zero,
            0);

        using CFScope properties = new(propertiesRef);

        if (result != 0 || properties.IsNull) {
            return false;
        }

        bool found = false;
        Dictionary<string, nint> props = CoreFoundation.ToDictionary(properties);

        if (props.TryGetValue("PerformanceStatistics", out nint perfStatsRef)) {
            Dictionary<string, nint> perfStats = CoreFoundation.ToDictionary(perfStatsRef);

            if (perfStats.TryGetValue("Alloc system memory", out nint allocRef)) {
                CoreFoundation.CFNumberGetValue(allocRef, out allocMemory);
                found = true;
            }

            if (perfStats.TryGetValue("In use system memory", out nint inUseRef)) {
                CoreFoundation.CFNumberGetValue(inUseRef, out inUseMemory);
                found = true;
            }
        }

        return found;
    }
}
#endif
