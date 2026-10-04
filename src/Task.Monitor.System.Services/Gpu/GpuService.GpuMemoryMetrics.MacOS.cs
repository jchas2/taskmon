#if __APPLE__
namespace Task.Monitor.System.Services.Gpu;

public partial class GpuService
{
    // Runs after OnDoWorkGpuPidMetrics, so GpuPercentTime is already set when the device metric is
    // emitted below.
    private bool OnDoWorkGpuMemoryMetrics(GpuInfo gpuInfo)
    {
        if (!TryReadIOAcceleratorMemory(out long allocMemory, out long inUseMemory)) {
            return false;
        }

        long available = allocMemory - inUseMemory;

        GpuMetrics metrics = gpuInfo.Metrics;
        metrics.TotalGpuMemory = allocMemory;
        metrics.AvailableGpuMemory = available;

        // Apple Silicon uses a single unified memory pool with no dedicated VRAM, so the shared and
        // combined figures mirror the one allocation pool.
        metrics.TotalSharedGpuMemory = allocMemory;
        metrics.AvailableSharedGpuMemory = available;
        metrics.TotalCombinedGpuMemory = allocMemory;
        metrics.AvailableCombinedGpuMemory = available;

        metrics.Devices.Add(new GpuDeviceMetrics {
            Index                      = 0,
            GpuPercentTime             = metrics.GpuPercentTime,
            TotalGpuMemory             = allocMemory,
            AvailableGpuMemory         = available,
            TotalSharedGpuMemory       = allocMemory,
            AvailableSharedGpuMemory   = available,
            TotalCombinedGpuMemory     = allocMemory,
            AvailableCombinedGpuMemory = available
        });

        return true;
    }
}
#endif
