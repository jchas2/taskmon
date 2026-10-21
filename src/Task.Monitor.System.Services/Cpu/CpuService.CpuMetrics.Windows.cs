using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Common;

namespace Task.Monitor.System.Services.Cpu;

public partial class CpuService
{
#if __WIN32__
    private CpuSystemTimes prevTimes  = new();
    private CpuSystemTimes currTimes  = new();
    private CpuSystemTimes deltaTimes = new();

    private long previousTimestamp;
    private bool primed;

    private PdhQuery? processorQuery;

    private unsafe void OnDoWorkCpuCore(CpuInfo cpuInfo)
    {
        if (processorQuery is null || !processorQuery.Collect()) {
            serviceStatus = ServiceStatus.Errored;
            return;
        }

        if (!processorQuery.TryReadFormattedDoubles(processorQuery.Counters[0], out uint itemCount)) {
            return;
        }

        Pdh.PDH_FMT_COUNTERVALUE_ITEM_W* items = (Pdh.PDH_FMT_COUNTERVALUE_ITEM_W*)processorQuery.Buffer;

        ImmutableArray<CpuInfo.CpuCoreMetric>.Builder arrayBuilder =
            ImmutableArray.CreateBuilder<CpuInfo.CpuCoreMetric>((int)itemCount);

        for (uint i = 0; i < itemCount; i++) {
            string name = Marshal.PtrToStringUni(items[i].szName) ?? string.Empty;

            if (!Pdh.IsTotalInstance(name)) {
                arrayBuilder.Add(new CpuInfo.CpuCoreMetric(name, items[i].doubleValue / 100));
            }
        }

        arrayBuilder.Sort(static (left, right) => CpuCoreMetricOrdering.Compare(left.Name, right.Name));
        cpuInfo.CoreMetrics = arrayBuilder.ToImmutable();
    }

    private unsafe void OnDoWorkCpuMetrics(CpuInfo cpuInfo)
    {
        MinWinBase.FILETIME lpIdleFileTime;
        MinWinBase.FILETIME lpKernelFileTime;
        MinWinBase.FILETIME lpUserFileTime;

        if (!ProcessThreadsApi.GetSystemTimes( 
            &lpIdleFileTime,
            &lpKernelFileTime,
            &lpUserFileTime)) {
            
            PInvokeErrorHelpers.AssertOnLastError(nameof(ProcessThreadsApi.GetSystemTimes));
            PInvokeErrorHelpers.TraceOnceOnLastError(nameof(ProcessThreadsApi.GetSystemTimes));

            cpuInfo.Metrics.CpuPercentIdleTime   = 0;
            cpuInfo.Metrics.CpuPercentKernelTime = 0;
            cpuInfo.Metrics.CpuPercentUserTime   = 0;
            cpuInfo.Metrics.CpuPercentIdleTime   = 0;
            return;
        }

        currTimes.Idle   = lpIdleFileTime.ToLong();
        currTimes.Kernel = lpKernelFileTime.ToLong() - currTimes.Idle; // On Windows, Kernel time also includes Idle time.
        currTimes.User   = lpUserFileTime.ToLong();

        long now = Stopwatch.GetTimestamp();

        if (!primed) {
            Rebase(now);
            primed = true;
            return;
        }

        double elapsedSeconds = (now - previousTimestamp) / (double)Stopwatch.Frequency;

        if (elapsedSeconds <= 0.0) {
            return;
        }

        deltaTimes.Idle   = currTimes.Idle - prevTimes.Idle;
        deltaTimes.Kernel = currTimes.Kernel - prevTimes.Kernel;
        deltaTimes.User   = currTimes.User - prevTimes.User;

        double totalSysTime = Environment.ProcessorCount * elapsedSeconds * CounterRate.FileTimeTicksPerSecond;

        cpuInfo.Metrics.CpuPercentUserTime   = deltaTimes.User / totalSysTime;
        cpuInfo.Metrics.CpuPercentKernelTime = deltaTimes.Kernel / totalSysTime;
        cpuInfo.Metrics.CpuPercentIdleTime   = deltaTimes.Idle / totalSysTime;
        cpuInfo.Metrics.CpuTotalTime         = cpuInfo.Metrics.CpuPercentKernelTime + cpuInfo.Metrics.CpuPercentUserTime;

        Rebase(now);
    }

    private void OnStartCpuCore()
    {
        const string ProcessorTimeCounterPath = @"\Processor(*)\% Processor Time";

        // Collected once up front: a rate counter needs two samples before it reports a value.
        processorQuery = PdhQuery.Open(nameof(CpuService), ProcessorTimeCounterPath);

        if (processorQuery is null || !processorQuery.Collect()) {
            serviceStatus = ServiceStatus.Errored;
        }
    }

    private void OnStopCpuCore()
    {
        processorQuery?.Dispose();
        processorQuery = null;
    }

    private void Rebase(long now)
    {
        prevTimes.Idle    = currTimes.Idle;
        prevTimes.Kernel  = currTimes.Kernel;
        prevTimes.User    = currTimes.User;
        previousTimestamp = now;
    }
#endif
}
