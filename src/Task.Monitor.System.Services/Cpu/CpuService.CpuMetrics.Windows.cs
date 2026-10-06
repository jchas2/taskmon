using System.Diagnostics;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
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

    private uint pdhResult = 0;
    nint hQuery = 0; 
    nint hCounter = 0;

    private unsafe void OnStartCpuCore()
    {
        const string ProcessorTimeCounterPath = @"\Processor(*)\% Processor Time";

        fixed (nint* phQuery = &hQuery, phCounter = &hCounter) {
            if ((pdhResult = Pdh.PdhOpenQuery(
                null,
                nint.Zero,
                phQuery)) != Pdh.ERROR_SUCCESS) {

                PInvokeErrorHelpers.TraceOnceOnPInvokeError(
                    nameof(Pdh.PdhOpenQuery),
                    $"Failed {nameof(CpuService)}",
                    pdhResult);

                serviceStatus = ServiceStatus.Errored;
                return;
            }

            if ((pdhResult = Pdh.PdhAddEnglishCounter(
                *phQuery,
                ProcessorTimeCounterPath,
                nint.Zero,
                phCounter)) != Pdh.ERROR_SUCCESS) {

                PInvokeErrorHelpers.TraceOnceOnPInvokeError(
                    ProcessorTimeCounterPath,
                    $"Failed {nameof(Pdh.PdhAddEnglishCounter)}",
                    pdhResult);

                Pdh.PdhCloseQuery(*phQuery);
                serviceStatus = ServiceStatus.Errored;
                return;
            }
            
            if ((pdhResult = Pdh.PdhCollectQueryData(*phQuery)) != Pdh.ERROR_SUCCESS) {
                PInvokeErrorHelpers.TraceOnceOnPInvokeError(
                    nameof(Pdh.PdhCollectQueryData), 
                    $"Failed {nameof(CpuService)}", 
                    pdhResult);

                Pdh.PdhCloseQuery(*phQuery);
                serviceStatus = ServiceStatus.Errored;
            }
        }
    }
    
    private unsafe void OnDoWorkCpuCore(CpuInfo cpuInfo)
    {
        fixed (nint* phQuery = &hQuery, phCounter = &hCounter) {
            if ((pdhResult = Pdh.PdhCollectQueryData(*phQuery)) != Pdh.ERROR_SUCCESS) {
                PInvokeErrorHelpers.TraceOnceOnPInvokeError(
                    nameof(Pdh.PdhCollectQueryData), 
                    $"Failed {nameof(CpuService)}", 
                    pdhResult);

                Pdh.PdhCloseQuery(*phQuery);
                serviceStatus = ServiceStatus.Errored;
                return;
            }
            
            uint bufferSize = 0;
            uint itemCount = 0;

            _ = Pdh.PdhGetFormattedCounterArrayW(
                *phCounter,
                Pdh.PDH_FMT_DOUBLE,
                &bufferSize, 
                &itemCount, 
                nint.Zero);

            if (bufferSize <= 0) {
                TraceEx.WriteLineOnce(
                    $"{nameof(Pdh.PdhGetFormattedCounterArrayW)} {*phCounter}", 
                    $"{nameof(Pdh.PdhGetFormattedCounterArrayW)} failed to calculate {nameof(bufferSize)} ");
            
                Pdh.PdhCloseQuery(*phQuery);
                serviceStatus = ServiceStatus.Errored;
                return;
            }

            nint buffer = Marshal.AllocHGlobal((int)bufferSize);

            if (Pdh.PdhGetFormattedCounterArrayW(
                *phCounter,
                Pdh.PDH_FMT_DOUBLE,
                &bufferSize,
                &itemCount,
                buffer) == Pdh.ERROR_SUCCESS) {

                int itemSize = Marshal.SizeOf<Pdh.PDH_FMT_COUNTERVALUE_ITEM_W>();

                ImmutableArray<CpuInfo.CpuCoreMetric>.Builder arrayBuilder = 
                    ImmutableArray.CreateBuilder<CpuInfo.CpuCoreMetric>();
                
                for (uint i = 0; i < itemCount; i++) {
                    nint itemPtr = nint.Add(buffer, (int)(i * itemSize));
                    
                    Pdh.PDH_FMT_COUNTERVALUE_ITEM_W item =
                        Marshal.PtrToStructure<Pdh.PDH_FMT_COUNTERVALUE_ITEM_W>(itemPtr);
                    
                    string name = Marshal.PtrToStringUni(item.szName) ?? string.Empty;

                    if (!Pdh.IsTotalInstance(name)) {
                        arrayBuilder.Add(new CpuInfo.CpuCoreMetric(name, item.doubleValue / 100));   
                    }
                }

                arrayBuilder.Sort(static (left, right) => CpuCoreMetricOrdering.Compare(left.Name, right.Name));
                cpuInfo.CoreMetrics = arrayBuilder.ToImmutable();
            }
            else {
                PInvokeErrorHelpers.TraceOnceOnPInvokeError(
                    $"{nameof(Pdh.PdhGetFormattedCounterArrayW)} {*phCounter}", 
                    $"{nameof(Pdh.PdhGetFormattedCounterArrayW)} failed to allocate {nameof(buffer)}", 
                    pdhResult);
            }
            
            Marshal.FreeHGlobal(buffer);
        }
    }

    private unsafe void OnStopCpuCore()
    {
        fixed (nint* phQuery = &hQuery) {
            Pdh.PdhCloseQuery(*phQuery);
        }
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
