#if __APPLE__
namespace Task.Monitor.System.Services.Cpu;

// macOS stub: the service runs and publishes empty specs and metrics until a macOS implementation
// replaces these.
public sealed partial class CpuService
{
    private void OnStartCpuSpecs(ref CpuSpecs specs) { }

    private void OnStartCpuCore() { }

    private void OnDoWorkCpuCore(CpuInfo cpuInfo) { }

    private void OnDoWorkCpuMetrics(CpuInfo cpuInfo) { }

    private void OnStopCpuCore() { }
}
#endif
