#if __APPLE__
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Process;

public sealed partial class ProcessService
{
    private void OnStartProcessSpecs(ProcessSpecs specs)
    {
        specs.LogicalProcessorCount = Environment.ProcessorCount;
        specs.IrixMode = IrixMode;
    }

    private void OnDoWorkProcessMetrics(ProcessMetrics metrics, ProcessSpecs specs)
    {
        List<ProcessSample> samples = GetProcessSamples();

        // Per-process GPU percent is published by GpuService (GpuInfo); absent a registered/primed
        // GpuService it falls back to the empty map, leaving ProcessEntry.GpuTimePercent at 0.
        Dictionary<int, double> gpuPercentByPid =
            GetLatest<GpuInfo>()?.Metrics.ProcessPercentTime ?? NoGpuPercent;

        BuildMetrics(metrics, specs, samples, gpuPercentByPid);
    }
}
#endif
