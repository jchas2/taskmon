#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Gpu;

// Shared macOS helpers for the GpuService partials. Per-process metrics and device-wide utilisation
// live in GpuService.GpuPidMetrics.MacOS.cs, specs in GpuService.GpuSpecs.MacOS.cs and memory metrics
// in GpuService.GpuMemoryMetrics.MacOS.cs.
public sealed partial class GpuService
{
    private const string IOServicePlane = "IOService";
    private const uint RegistryIterateRecursively = 0x00000001;

    // Reads the IOAccelerator PerformanceStatistics allocation figures. These describe the single
    // unified GPU allocation pool on Apple Silicon (bytes).
    private static bool TryReadIOAcceleratorMemory(out long allocMemory, out long inUseMemory)
    {
        allocMemory = 0;
        inUseMemory = 0;

        IntPtr matching = IOKit.IOServiceMatching("IOAccelerator");
        uint accelerator = IOKit.IOServiceGetMatchingService(0, matching);

        if (accelerator == 0) {
            return false;
        }

        int result = IOKit.IORegistryEntryCreateCFProperties(
            accelerator,
            out IntPtr propertiesRef,
            IntPtr.Zero,
            0);

        using CFScope properties = new(propertiesRef);

        if (result != 0 || properties.IsNull) {
            IOKit.IOObjectRelease(accelerator);
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

        IOKit.IOObjectRelease(accelerator);
        return found;
    }
}
#endif
