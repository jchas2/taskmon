using System.Diagnostics;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Cpu;

public partial class CpuService
{
#if __WIN32__
    // GetSystemTimes reports FILETIME ticks, which are 100ns units.
    private const double FileTimeTicksPerSecond = 10_000_000.0;

    private CpuSystemTimes prevTimes = new();
    private CpuSystemTimes currTimes = new();
    private CpuSystemTimes deltaTimes = new();

    private long previousTimestamp;
    private bool primed;

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
