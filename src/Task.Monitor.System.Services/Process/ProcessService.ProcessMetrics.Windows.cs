using System.Diagnostics;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.WindowsServices;

namespace Task.Monitor.System.Services.Process;

public partial class ProcessService
{
#if __WIN32__
    private void LogFirstSample(int opened, Dictionary<int, int> openFailures)
    {
        if (opened == 0) {
            Trace.WriteLine($"{ServiceName}: process snapshot FAILED, no processes could be read");
            return;
        }

        Trace.WriteLine($"{ServiceName}: process snapshot OK, {opened} processes opened");

        if (openFailures.Count > 0) {
            Trace.WriteLine(
                $"{ServiceName}: {openFailures.Values.Sum()} processes could not be opened: {DescribeFailures(openFailures)}");
        }
    }

    private void OnDoWorkProcessMetrics(ProcessMetrics metrics, ProcessSpecs specs)
    {
        WindowsServiceLookup.RefreshIfDue();

        Dictionary<int, int>? openFailures = IsFirstCycle ? new() : null;
        List<ProcessSample>   samples      = GetProcessSamples(openFailures);

        if (openFailures != null) {
            LogFirstSample(samples.Count, openFailures);
        }

        Dictionary<int, double> gpuPercentByPid =
            GetLatest<GpuInfo>()?.Metrics.ProcessPercentTime ?? NoGpuPercent;

        BuildMetrics(metrics, specs, samples, gpuPercentByPid);

        WindowsServiceLookup.RequestRefreshIfStale(sampleStates.Keys);
    }
#endif
}
