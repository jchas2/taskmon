#if __APPLE__
using System.Diagnostics;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Gpu;

public partial class GpuService
{
    private const string WiredLimitKey = "iogpu.wired_limit_mb";

    private ulong? recommendedWorkingSet;
    private bool   recommendedWorkingSetRead;

    internal static long GpuMemoryLimit(long wiredLimitMb, ulong? recommendedWorkingSet, long allocatedMemory)
    {
        if (wiredLimitMb > 0) {
            return wiredLimitMb * 1024 * 1024;
        }

        return recommendedWorkingSet is { } recommended ? (long)recommended : allocatedMemory;
    }

    private bool OnDoWorkGpuMemoryMetrics(GpuInfo gpuInfo)
    {
        if (!TryReadIOAcceleratorMemory(out long allocMemory, out long inUseMemory)) {
            if (IsFirstCycle) {
                Trace.WriteLine($"{ServiceName}: GPU memory metrics SKIPPED, no IOAccelerator memory statistics");
            }

            return false;
        }

        if (!recommendedWorkingSetRead) {
            recommendedWorkingSet     = Metal.RecommendedMaxWorkingSetSize();
            recommendedWorkingSetRead = true;

            Trace.WriteLine(recommendedWorkingSet is { } workingSet
                ? $"{ServiceName}: Metal recommendedMaxWorkingSetSize OK, {workingSet / (1024 * 1024)} MB"
                : $"{ServiceName}: Metal recommendedMaxWorkingSetSize NOT FOUND, using the memory allocated so far");
        }

        long wiredLimitMb = Sys.SysctlByNameInt(WiredLimitKey, out int limit) ? limit : 0;
        long totalMemory  = Math.Max(GpuMemoryLimit(wiredLimitMb, recommendedWorkingSet, allocMemory), inUseMemory);
        long available    = totalMemory - inUseMemory;

        GpuMetrics metrics = gpuInfo.Metrics;
        metrics.TotalGpuMemory             = totalMemory;
        metrics.AvailableGpuMemory         = available;

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
