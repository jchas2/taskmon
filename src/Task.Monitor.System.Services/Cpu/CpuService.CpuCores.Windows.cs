using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Cpu;

public partial class CpuService
{
#if __WIN32__
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

                    if (!name.Equals("_Total", StringComparison.OrdinalIgnoreCase)) {
                        arrayBuilder.Add(new CpuInfo.CpuCoreMetric(name, item.doubleValue / 100));   
                    }
                }

                // PDH returns the \Processor(*) instances in lexicographic order (0, 1, 10, 2, ...).
                // Sort by the core index so consumers that bind positionally show 0..N in order.
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
#endif
}