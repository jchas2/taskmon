using System.Diagnostics;
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Process;

public partial class ProcessService
{
#if __APPLE__
    private void OnDoWorkProcessMetrics(ProcessMetrics metrics, ProcessSpecs specs)
    {
        List<ProcessSample> samples = GetProcessSamples();

        if (IsFirstCycle) {
            Trace.WriteLine(samples.Count > 0
                ? $"{ServiceName}: process list OK, {samples.Count} processes read"
                : $"{ServiceName}: process list FAILED, no processes read");
        }

        Dictionary<int, double> gpuPercentByPid =
            GetLatest<GpuInfo>()?.Metrics.ProcessPercentTime ?? NoGpuPercent;

        BuildMetrics(metrics, specs, samples, gpuPercentByPid);
    }
#endif
}
