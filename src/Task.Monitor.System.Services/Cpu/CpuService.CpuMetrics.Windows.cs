using System.Diagnostics;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Cpu;

public partial class CpuService
{
    // GetSystemTimes reports FILETIME ticks, which are 100ns units.
    private const double FileTimeTicksPerSecond = 10_000_000.0;

    private CpuSystemTimes prevTimes = new();
    private CpuSystemTimes currTimes = new();
    private CpuSystemTimes deltaTimes = new();

    private long previousTimestamp;
    private bool primed;

#if __WIN32__
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

            cpuInfo.Metrics.CpuPercentIdleTime = 0;
            cpuInfo.Metrics.CpuPercentKernelTime = 0;
            cpuInfo.Metrics.CpuPercentUserTime = 0;
            cpuInfo.Metrics.CpuPercentIdleTime = 0;
            return;
        }

        currTimes.Idle   = lpIdleFileTime.ToLong();
        currTimes.Kernel = lpKernelFileTime.ToLong() - currTimes.Idle; // On Windows, Kernel time also includes Idle time.
        currTimes.User   = lpUserFileTime.ToLong();

        long now = Stopwatch.GetTimestamp();

        // GetSystemTimes is cumulative since boot, so the first reading has nothing to difference
        // against. Differencing it against zero reports the machine's entire uptime as though it
        // happened in one interval. This cycle publishes the zeros CpuMetrics starts with and
        // establishes the baseline instead.
        if (!primed) {
            Rebase(now);
            primed = true;
            return;
        }

        double elapsedSeconds = (now - previousTimestamp) / (double)Stopwatch.Frequency;

        // No interval means no rate. The baseline is deliberately left where it is: moving it
        // without advancing the timestamp would fold this cycle's cpu time into the baseline where
        // no later delta could see it.
        if (elapsedSeconds <= 0.0) {
            return;
        }

        deltaTimes.Idle   = currTimes.Idle - prevTimes.Idle;
        deltaTimes.Kernel = currTimes.Kernel - prevTimes.Kernel;
        deltaTimes.User   = currTimes.User - prevTimes.User;

        // Measured, not the nominal Delay. A cycle actually takes Delay plus however long the
        // sampling itself took, so dividing by Delay overstated every percentage by that fraction;
        // and once Delay became changeable at runtime, the cycle spanning a change would have been
        // divided by an interval it was never measured over.
        double totalSysTime = Environment.ProcessorCount * elapsedSeconds * FileTimeTicksPerSecond;

        cpuInfo.Metrics.CpuPercentUserTime   = deltaTimes.User / totalSysTime;
        cpuInfo.Metrics.CpuPercentKernelTime = deltaTimes.Kernel / totalSysTime;
        cpuInfo.Metrics.CpuPercentIdleTime   = deltaTimes.Idle / totalSysTime;
        cpuInfo.Metrics.CpuTotalTime         = cpuInfo.Metrics.CpuPercentKernelTime + cpuInfo.Metrics.CpuPercentUserTime;

        Rebase(now);
    }

    private void Rebase(long now)
    {
        prevTimes.Idle   = currTimes.Idle;
        prevTimes.Kernel = currTimes.Kernel;
        prevTimes.User   = currTimes.User;
        previousTimestamp = now;
    }
#endif
}
