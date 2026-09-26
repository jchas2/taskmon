#if __APPLE__
namespace Task.Monitor.System.Services.Gpu;

// macOS stub: the service runs and publishes empty specs and metrics until a macOS implementation
// replaces these.
public sealed partial class GpuService
{
    private void OnStartGpuSpecs(GpuSpecs specs) { }

    private void OnStartGpuPidMetrics() { }

    private void OnDoWorkGpuPidMetrics(GpuInfo gpuInfo) { }

    private bool OnDoWorkGpuMemoryMetrics(GpuInfo gpuInfo) => false;

    private void OnStopGpuPidMetrics() { }
}
#endif
