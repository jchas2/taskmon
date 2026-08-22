using System.Diagnostics;
using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Process;

public static partial class GpuService
{
#if __WIN32__
#pragma warning disable CA1416

    private const string GpuEngineCounterPath = @"\GPU Engine(*)\Running Time";

    private static readonly Dictionary<string, long> prevEngineValues = new();
    private static readonly Dictionary<int, long> cumulativeEngineValues = new();

    private static unsafe Dictionary<int, long> GetProcessStatsInternal()
    {
        uint pdhResult = 0;
        nint hQuery = 0;
        nint hCounter = 0;
        
        if ((pdhResult = Pdh.PdhOpenQuery(
            null, 
            nint.Zero, 
            &hQuery)) != Pdh.ERROR_SUCCESS) {

            PInvokeErrorHelpers.TraceOnceOnPInvokeError(
                nameof(Pdh.PdhOpenQuery), 
                $"Failed {nameof(GetProcessStatsInternal)}", 
                pdhResult);
            
            return new Dictionary<int, long>(cumulativeEngineValues);
        }

        if ((pdhResult = Pdh.PdhAddEnglishCounter(
            hQuery,
            GpuEngineCounterPath,
            nint.Zero,
            &hCounter)) != Pdh.ERROR_SUCCESS) {
            
            PInvokeErrorHelpers.TraceOnceOnPInvokeError(
                GpuEngineCounterPath,
                $"Failed {nameof(Pdh.PdhAddEnglishCounter)}", 
                pdhResult);

            Pdh.PdhCloseQuery(hQuery);
            return new Dictionary<int, long>(cumulativeEngineValues);
        }

        if ((pdhResult = Pdh.PdhCollectQueryData(hQuery)) != Pdh.ERROR_SUCCESS) {
            PInvokeErrorHelpers.TraceOnceOnPInvokeError(
                nameof(Pdh.PdhCollectQueryData), 
                $"Failed {nameof(GetProcessStatsInternal)}", 
                pdhResult);

            Pdh.PdhCloseQuery(hQuery);
            return new Dictionary<int, long>(cumulativeEngineValues);
        }

        uint bufferSize = 0;
        uint itemCount = 0;

        _ = Pdh.PdhGetRawCounterArray(
            hCounter, 
            &bufferSize, 
            &itemCount, 
            nint.Zero);

        if (bufferSize <= 0) {
            TraceEx.WriteLineOnce(
                $"{nameof(Pdh.PdhGetRawCounterArray)} {hCounter}", 
                $"{nameof(Pdh.PdhGetRawCounterArray)} failed to calculate {nameof(bufferSize)} ");
            
            Pdh.PdhCloseQuery(hQuery);
            return new Dictionary<int, long>(cumulativeEngineValues);
        }

        nint buffer = Marshal.AllocHGlobal((int)bufferSize);

        if (Pdh.PdhGetRawCounterArray(
            hCounter, 
            &bufferSize, 
            &itemCount, 
            buffer) == Pdh.ERROR_SUCCESS) {
            
            int structSize = Marshal.SizeOf<Pdh.PDH_RAW_COUNTER_ITEM>();
            Dictionary<int, long> maxDeltas = new();

            for (int i = 0; i < itemCount; i++) {
                nint currentItemPtr = new nint(buffer.ToInt64() + (i * structSize));
                Pdh.PDH_RAW_COUNTER_ITEM item = Marshal.PtrToStructure<Pdh.PDH_RAW_COUNTER_ITEM>(currentItemPtr);

                if (item.RawValue.CStatus != Pdh.PDH_CSTATUS_VALID_DATA || 
                    item.RawValue.FirstValue <= 0) {
                    continue;
                }

                if (!IsTrackedEngineType(item.szName)) {
                    continue;
                }

                int pid = ParsePidFromInstance(item.szName);

                if (pid < 0) {
                    continue;
                }

                long current = item.RawValue.FirstValue;
                long prev = prevEngineValues.GetValueOrDefault(item.szName, current);
                long delta = current - prev;
                
                prevEngineValues[item.szName] = current;

                if (delta > 0) {
                    maxDeltas[pid] = Math.Max(maxDeltas.GetValueOrDefault(pid, 0), delta);
                }
            }

            foreach (var (pid, maxDelta) in maxDeltas) {
                cumulativeEngineValues[pid] = cumulativeEngineValues.GetValueOrDefault(pid, 0) + maxDelta;
            }
        }
        else {
            PInvokeErrorHelpers.TraceOnceOnPInvokeError(
                $"{nameof(Pdh.PdhGetRawCounterArray)} {hCounter}", 
                $"{nameof(Pdh.PdhGetRawCounterArray)} failed to allocate {nameof(buffer)}", 
                pdhResult);
        }
            
        Marshal.FreeHGlobal(buffer);
        Pdh.PdhCloseQuery(hQuery);

        return new Dictionary<int, long>(cumulativeEngineValues);
    }

    private static bool IsTrackedEngineType(string instanceName) =>
        instanceName.EndsWith("_engtype_3D",           StringComparison.Ordinal) ||
        instanceName.EndsWith("_engtype_VideoDecode",  StringComparison.Ordinal) ||
        instanceName.EndsWith("_engtype_VideoEncode",  StringComparison.Ordinal) ||
        instanceName.EndsWith("_engtype_Copy",         StringComparison.Ordinal);

    private static int ParsePidFromInstance(string instanceName)
    {
        // Format example: pid_1234_luid_0x00000000_phys_0_eng_3_engtype_3D
        const string pidPrefix = "pid_";
        int pidIndex = instanceName.IndexOf(pidPrefix, StringComparison.OrdinalIgnoreCase);

        if (pidIndex == -1) {
            return -1;
        }

        int startIndex = pidIndex + pidPrefix.Length;

        if (startIndex >= instanceName.Length) {
            return -1;
        }

        int endIndex = instanceName.IndexOf('_', startIndex);

        if (endIndex == -1) {
            endIndex = instanceName.Length;
        }

        ReadOnlySpan<char> pidSpan = instanceName.AsSpan(startIndex, endIndex - startIndex);

        if (int.TryParse(pidSpan, out int pid)) {
            return pid;
        }

        return -1;
    }

#pragma warning restore CA1416
#endif
}
