#if __APPLE__
namespace Task.Monitor.System.Services.Process;

// macOS stub: the service runs and publishes empty specs and metrics until a macOS implementation
// replaces these.
public sealed partial class ProcessService
{
    private void OnStartProcessSpecs(ProcessSpecs specs) { }

    private void OnDoWorkProcessMetrics(ProcessMetrics metrics, ProcessSpecs specs) { }

    private void OnStopProcessMetrics() { }
}
#endif
