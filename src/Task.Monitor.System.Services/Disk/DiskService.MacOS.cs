#if __APPLE__
namespace Task.Monitor.System.Services.Disk;

// macOS stub: the service runs and publishes empty specs and metrics until a macOS implementation
// replaces these.
public sealed partial class DiskService
{
    private void OnStartDiskSpecs(DiskSpecs specs) { }

    private void OnStartDiskMetrics() { }

    private void OnDoWorkDiskMetrics(DiskInfo diskInfo) { }

    private void OnStopDiskMetrics() { }
}
#endif
