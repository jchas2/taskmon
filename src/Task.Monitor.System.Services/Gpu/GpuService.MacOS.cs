#if __APPLE__
using Task.Monitor.Interop.Mach;
using Task.Monitor.System.Native;

namespace Task.Monitor.System.Services.Gpu;

public sealed partial class GpuService
{
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
        Dictionary<string, nint> props = CFConvert.ToDictionary(properties);

        if (props.TryGetValue("PerformanceStatistics", out nint perfStatsRef)) {
            Dictionary<string, nint> perfStats = CFConvert.ToDictionary(perfStatsRef);

            if (perfStats.TryGetValue("Alloc system memory", out nint allocRef)) {
                allocMemory = CFConvert.GetInt64(allocRef);
                found = true;
            }

            if (perfStats.TryGetValue("In use system memory", out nint inUseRef)) {
                inUseMemory = CFConvert.GetInt64(inUseRef);
                found = true;
            }
        }

        return found;
    }
}
#endif
