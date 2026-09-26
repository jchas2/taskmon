#if __APPLE__
namespace Task.Monitor.System.Services.Memory;

// macOS stub: the service runs and publishes empty specs and metrics until a macOS implementation
// replaces these.
public sealed partial class MemoryService
{
    private void OnStartMemorySpecs(MemorySpecs specs) { }

    private void OnDoWorkMemoryMetrics(MemoryInfo memoryInfo) { }

    private void OnDoWorkMemoryCompressionMetrics(MemoryInfo memoryInfo) { }
}
#endif
