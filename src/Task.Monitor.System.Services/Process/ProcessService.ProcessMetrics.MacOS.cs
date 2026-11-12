using System.Diagnostics;
using Task.Monitor.System.Services.Gpu;

namespace Task.Monitor.System.Services.Process;

public partial class ProcessService
{
#if __APPLE__
    private void LogFirstSample(int read, bool listed, Dictionary<int, int> readFailures)
    {
        Trace.WriteLine(listed
            ? $"{ServiceName}: proc_listallpids OK, {read} processes read"
            : $"{ServiceName}: proc_listallpids FAILED, showing this process only");

        if (readFailures.Count > 0) {
            Trace.WriteLine(
                $"{ServiceName}: {readFailures.Values.Sum()} processes could not be read: {DescribeFailures(readFailures)}");
        }
    }

    private void OnDoWorkProcessMetrics(ProcessMetrics metrics, ProcessSpecs specs)
    {
        Dictionary<int, int>? readFailures = IsFirstCycle ? new() : null;
        List<ProcessSample>   samples      = GetProcessSamples(readFailures, out bool listed);

        if (readFailures != null) {
            LogFirstSample(samples.Count, listed, readFailures);
        }

        Dictionary<int, double> gpuPercentByPid =
            GetLatest<GpuInfo>()?.Metrics.ProcessPercentTime ?? NoGpuPercent;

        BuildMetrics(metrics, specs, samples, gpuPercentByPid);
    }
#endif
}
