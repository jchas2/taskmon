#if __APPLE__
using System.Diagnostics;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Mach;
using Task.Monitor.System.Services.Common;

namespace Task.Monitor.System.Services.Disk;

public partial class DiskService
{
    private const double NanosecondsPerMillisecond = 1_000_000.0;
    private const double NanosecondsPerSecond      = 1_000_000_000.0;

    private readonly Dictionary<ulong, bool> virtualDiskEntries = new();

    private void ApplyDiskSample(
        string bsdName,
        int    index,
        long   readBytes,
        long   writeBytes,
        long   busyNanos,
        long   operations)
    {
        long now = Stopwatch.GetTimestamp();

        if (!states.TryGetValue(bsdName, out DiskState? state)) {
            states[bsdName] = new DiskState {
                Index              = index,
                PreviousReadBytes  = readBytes,
                PreviousWriteBytes = writeBytes,
                PreviousTime       = busyNanos,
                PreviousOperations = operations,
                PreviousTimestamp  = now
            };

            return;
        }

        double elapsedSeconds = Stopwatch.GetElapsedTime(state.PreviousTimestamp, now).TotalSeconds;

        if (elapsedSeconds <= 0.0) {
            return;
        }

        double busy            = Math.Max(0, busyNanos - state.PreviousTime);
        long   operationsDelta = operations - state.PreviousOperations;

        state.Index               = index;
        state.ReadBytesPerSecond  = CounterRate.Accumulate(readBytes, ref state.PreviousReadBytes, ref state.TotalBytesRead, elapsedSeconds);
        state.WriteBytesPerSecond = CounterRate.Accumulate(writeBytes, ref state.PreviousWriteBytes, ref state.TotalBytesWritten, elapsedSeconds);
        state.PercentActiveTime   = Math.Clamp(100.0 * busy / (elapsedSeconds * NanosecondsPerSecond), 0.0, 100.0);
        state.LatencyMs           = operationsDelta > 0 ? busy / operationsDelta / NanosecondsPerMillisecond : 0.0;
        state.PreviousTime        = busyNanos;
        state.PreviousOperations  = operations;
        state.PreviousTimestamp   = now;
    }

    private bool IsVirtualDisk(uint entry, HashSet<ulong> seenEntries)
    {
        if (IOKit.IORegistryEntryGetRegistryEntryID(entry, out ulong entryId) != 0) {
            return IsVirtualInterconnect(ReadPhysicalInterconnect(entry));
        }

        seenEntries.Add(entryId);

        if (!virtualDiskEntries.TryGetValue(entryId, out bool isVirtual)) {
            isVirtual = IsVirtualInterconnect(ReadPhysicalInterconnect(entry));
            virtualDiskEntries[entryId] = isVirtual;
        }

        return isVirtual;
    }

    private void OnDoWorkDiskMetrics(DiskInfo diskInfo)
    {
        UpdateDiskStates();
        BuildMetrics(diskInfo.Metrics);
    }

    private void OnStopDiskMetrics() => virtualDiskEntries.Clear();

    private void ReadDriverStatistics(uint entry, HashSet<string> live)
    {
        string? bsdName = ReadBsdName(entry);
        int index = ParseDiskIndex(bsdName);

        if (index < 0 || bsdName == null) {
            return;
        }

        int result = IOKit.IORegistryEntryCreateCFProperties(entry, out IntPtr propertiesRef, IntPtr.Zero, 0);
        using CFScope properties = new(propertiesRef);

        if (result != 0 || properties.IsNull) {
            return;
        }

        Dictionary<string, nint> props = CoreFoundation.ToDictionary(properties);

        if (!props.TryGetValue("Statistics", out nint statsRef)) {
            return;
        }

        // Values below are borrowed from 'properties', which the scope keeps alive.
        Dictionary<string, nint> stats = CoreFoundation.ToDictionary(statsRef);

        live.Add(bsdName);

        ApplyDiskSample(
            bsdName,
            index,
            ReadStat(stats, "Bytes (Read)"),
            ReadStat(stats, "Bytes (Write)"),
            ReadStat(stats, "Total Time (Read)") + ReadStat(stats, "Total Time (Write)"),
            ReadStat(stats, "Operations (Read)") + ReadStat(stats, "Operations (Write)"));
    }

    private static long ReadStat(Dictionary<string, nint> stats, string key)
    {
        if (stats.TryGetValue(key, out nint number)) {
            CoreFoundation.CFNumberGetValue(number, out long value);
            return value;
        }

        return 0;
    }

    private void UpdateDiskStates()
    {
        int result = IOKit.IOServiceGetMatchingServices(0, IOKit.IOServiceMatching(BlockStorageDriverClass), out IntPtr iteratorRef);
        using IOObjectScope iterator = new(iteratorRef);

        if (result != 0 || iterator.IsNull) {
            string failure = result != 0
                ? $"{ServiceName}: IOKit {BlockStorageDriverClass} statistics FAILED {ErrorCodes.DescribeKernReturn(result)}"
                : $"{ServiceName}: IOKit {BlockStorageDriverClass} statistics NOT FOUND";

            if (IsFirstCycle) {
                Trace.WriteLine(failure);
            }

            TraceEx.WriteLineOnce($"{nameof(DiskService)} {BlockStorageDriverClass} statistics", failure);
            return;
        }

        HashSet<string> live        = new();
        HashSet<ulong>  seenEntries = new();
        uint entryRef;

        while ((entryRef = IOKit.IOIteratorNext(iterator)) != 0) {
            using IOObjectScope entry = new(entryRef);

            if (!IsVirtualDisk(entry, seenEntries)) {
                ReadDriverStatistics(entry, live);
            }
        }

        if (IsFirstCycle) {
            Trace.WriteLine($"{ServiceName}: IOKit {BlockStorageDriverClass} statistics OK, {live.Count} disks read");
        }

        PruneMissing(live.Contains);

        foreach (ulong stale in virtualDiskEntries.Keys.Where(entryId => !seenEntries.Contains(entryId)).ToList()) {
            virtualDiskEntries.Remove(stale);
        }
    }
}
#endif
