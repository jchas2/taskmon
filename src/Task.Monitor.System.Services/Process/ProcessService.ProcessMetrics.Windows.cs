using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Process;

public partial class ProcessService
{
#if __WIN32__
    private void OnDoWorkProcessMetrics(ProcessMetrics metrics, ProcessSpecs specs)
    {
        WindowsServiceLookup.RefreshIfDue();

        List<ProcessSample> samples = GetProcessSamples();

        Dictionary<int, double> gpuPercentByPid =
            GetLatest<GpuInfo>()?.Metrics.ProcessPercentTime ?? NoGpuPercent;

        BuildMetrics(metrics, specs, samples, gpuPercentByPid);

        WindowsServiceLookup.RequestRefreshIfStale(sampleStates.Keys);
    }
#endif
}
