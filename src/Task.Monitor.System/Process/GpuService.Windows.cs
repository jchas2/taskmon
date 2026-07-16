using System.Runtime.InteropServices;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Process;

public static partial class GpuService
{
#if __WIN32__
#pragma warning disable CA1416

    private const string GpuEngineCounterPath = @"\GPU Engine(*)\Running Time";

    private static readonly Dictionary<string, long> prevEngineValues = new();
    private static readonly Dictionary<int, long> cumulativeEngineValues = new();

    private static Dictionary<int, long> GetProcessStatsInternal()
    {
        if (Pdh.PdhOpenQuery(null, IntPtr.Zero, out IntPtr hQuery) != Pdh.ERROR_SUCCESS) {
            Trace.WriteLine($"Failed PdhOpenQuery in {nameof(GpuService)}.");
            return new Dictionary<int, long>(cumulativeEngineValues);
        }

        Pdh.PdhAddEnglishCounter(
            hQuery, 
            GpuEngineCounterPath, 
            IntPtr.Zero, 
            out IntPtr hCounter);
        
        Pdh.PdhCollectQueryData(hQuery);

        uint bufferSize = 0;
        uint itemCount = 0;

        Pdh.PdhGetRawCounterArray(
            hCounter, 
            ref bufferSize, 
            ref itemCount, 
            IntPtr.Zero);

        if (bufferSize <= 0) {
            Pdh.PdhCloseQuery(hQuery);
            return new Dictionary<int, long>(cumulativeEngineValues);
        }

        IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);

        if (Pdh.PdhGetRawCounterArray(
            hCounter, 
            ref bufferSize, 
            ref itemCount, 
            buffer) == Pdh.ERROR_SUCCESS) {
            
            int structSize = Marshal.SizeOf<Pdh.PDH_RAW_COUNTER_ITEM>();
            Dictionary<int, long> maxDeltas = new();

            for (int i = 0; i < itemCount; i++) {
                IntPtr currentItemPtr = new IntPtr(buffer.ToInt64() + (i * structSize));
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

        if (pidIndex == -1)
            return -1;

        int startIndex = pidIndex + pidPrefix.Length;
        if (startIndex >= instanceName.Length)
            return -1;

        int endIndex = instanceName.IndexOf('_', startIndex);
        if (endIndex == -1)
            endIndex = instanceName.Length;

        ReadOnlySpan<char> pidSpan = instanceName.AsSpan(startIndex, endIndex - startIndex);

        if (int.TryParse(pidSpan, out int pid))
            return pid;

        return -1;
    }

#pragma warning restore CA1416
#endif
}
