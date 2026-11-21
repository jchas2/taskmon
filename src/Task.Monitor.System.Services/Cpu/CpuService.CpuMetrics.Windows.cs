using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Native;
using Task.Monitor.System.Services.Common;

namespace Task.Monitor.System.Services.Cpu;

public partial class CpuService
{
#if __WIN32__
    private long previousIdle;
    private long previousKernel;
    private long previousUser;
    private long previousTimestamp;

    private PdhQuery? pressureQuery;
    private PdhQuery? processorQuery;

    private unsafe void OnDoWorkCpuCore(CpuInfo cpuInfo)
    {
        if (processorQuery is null || !processorQuery.Collect()) {
            Status = ServiceStatus.Errored;
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

            if (!PdhQuery.IsTotalInstance(name)) {
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
            return;
        }

        long idle   = lpIdleFileTime.ToLong();
        long kernel = lpKernelFileTime.ToLong() - idle; // On Windows, kernel time also includes idle time.
        long user   = lpUserFileTime.ToLong();
        long now    = Stopwatch.GetTimestamp();

        if (previousTimestamp != 0) {
            double totalSysTime = Environment.ProcessorCount *
                                  Stopwatch.GetElapsedTime(previousTimestamp, now).TotalSeconds *
                                  CounterRate.FileTimeTicksPerSecond;

            if (totalSysTime <= 0.0) {
                return;
            }

            cpuInfo.Metrics.CpuPercentUserTime   = (user - previousUser) / totalSysTime;
            cpuInfo.Metrics.CpuPercentKernelTime = (kernel - previousKernel) / totalSysTime;
            cpuInfo.Metrics.CpuPercentIdleTime   = (idle - previousIdle) / totalSysTime;
            cpuInfo.Metrics.CpuTotalTime         = cpuInfo.Metrics.CpuPercentKernelTime + cpuInfo.Metrics.CpuPercentUserTime;
        }

        previousIdle      = idle;
        previousKernel    = kernel;
        previousUser      = user;
        previousTimestamp = now;
    }

    private void OnDoWorkPressureMetrics(CpuInfo cpuInfo)
    {
        if (pressureQuery is null || !pressureQuery.Collect()) {
            return;
        }

        if (pressureQuery.TryReadFormattedDouble(pressureQuery.Counters[0], out double queueLength)) {
            cpuInfo.Metrics.RunQueuePerCore = queueLength / Environment.ProcessorCount;
        }

        if (pressureQuery.TryReadFormattedDouble(pressureQuery.Counters[1], out double performanceLimit)) {
            cpuInfo.Metrics.PerformanceLimitPercent = performanceLimit;
        }

        if (pressureQuery.TryReadFormattedDouble(pressureQuery.Counters[2], out double dpcTime) &&
            pressureQuery.TryReadFormattedDouble(pressureQuery.Counters[3], out double interruptTime)) {

            cpuInfo.Metrics.DriverTimePercent = dpcTime + interruptTime;
        }
    }

    private void OnStartCpuCore()
    {
        const string ProcessorTimeCounterPath = @"\Processor(*)\% Processor Time";

        processorQuery = PdhQuery.OpenAtStartup(ServiceName, ProcessorTimeCounterPath, ProcessorTimeCounterPath);

        if (processorQuery is null || !processorQuery.Collect()) {
            Status = ServiceStatus.Errored;
        }

        pressureQuery = PdhQuery.OpenAtStartup(
            ServiceName,
            "pressure counters",
            @"\System\Processor Queue Length",
            @"\Processor Information(_Total)\% Performance Limit",
            @"\Processor(_Total)\% DPC Time",
            @"\Processor(_Total)\% Interrupt Time");

        pressureQuery?.Collect();
    }

    private void OnStopCpuCore()
    {
        processorQuery?.Dispose();
        processorQuery = null;

        pressureQuery?.Dispose();
        pressureQuery = null;
    }
#endif
}
