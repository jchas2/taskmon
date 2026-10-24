#if __APPLE__
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Gpu;

public partial class GpuService
{
    private const string WiredLimitKey = "iogpu.wired_limit_mb";

    private ulong? recommendedWorkingSet;
    private bool   recommendedWorkingSetRead;

    // The GPU's ceiling in unified memory: the wired limit when an admin has raised it, otherwise
    // Metal's recommended working set, falling back to the pool allocated so far.
    internal static long GpuMemoryLimit(long wiredLimitMb, ulong? recommendedWorkingSet, long allocatedMemory)
    {
        if (wiredLimitMb > 0) {
            return wiredLimitMb * 1024 * 1024;
        }

        return recommendedWorkingSet is { } recommended ? (long)recommended : allocatedMemory;
    }

    // Runs after OnDoWorkGpuPidMetrics, so GpuPercentTime is already set when the device metric is
    // emitted below.
    private bool OnDoWorkGpuMemoryMetrics(GpuInfo gpuInfo)
    {
        if (!TryReadIOAcceleratorMemory(out long allocMemory, out long inUseMemory)) {
            return false;
        }

        if (!recommendedWorkingSetRead) {
            recommendedWorkingSet     = Metal.RecommendedMaxWorkingSetSize();
            recommendedWorkingSetRead = true;
        }

        long wiredLimitMb = Sys.SysctlByNameInt(WiredLimitKey, out int limit) ? limit : 0;
        long totalMemory  = Math.Max(GpuMemoryLimit(wiredLimitMb, recommendedWorkingSet, allocMemory), inUseMemory);
        long available    = totalMemory - inUseMemory;

        GpuMetrics metrics = gpuInfo.Metrics;
        metrics.TotalGpuMemory             = totalMemory;
        metrics.AvailableGpuMemory         = available;

        // Apple Silicon uses a single unified memory pool with no dedicated VRAM, so the shared and
        // combined figures all report the same limit.
        metrics.TotalSharedGpuMemory       = totalMemory;
        metrics.AvailableSharedGpuMemory   = available;
        metrics.TotalCombinedGpuMemory     = totalMemory;
        metrics.AvailableCombinedGpuMemory = available;

        metrics.Devices.Add(new GpuDeviceMetrics {
            Index                      = 0,
            GpuPercentTime             = metrics.GpuPercentTime,
            TotalGpuMemory             = totalMemory,
            AvailableGpuMemory         = available,
            TotalSharedGpuMemory       = totalMemory,
            AvailableSharedGpuMemory   = available,
            TotalCombinedGpuMemory     = totalMemory,
            AvailableCombinedGpuMemory = available
        });

        return true;
    }
}
#endif
