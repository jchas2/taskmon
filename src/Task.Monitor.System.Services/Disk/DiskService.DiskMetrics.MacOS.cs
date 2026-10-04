#if __APPLE__
using System.Diagnostics;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Disk;

public partial class DiskService
{
    private const double BytesPerMegabyte = 1024.0 * 1024.0;
    private const double NanosecondsPerSecond = 1_000_000_000.0;

    private sealed class DiskDeviceState
    {
        public int    Index;
        public long   PrevReadBytes;
        public long   PrevWriteBytes;
        public long   PrevReadTimeNanos;
        public long   PrevWriteTimeNanos;
        public long   PrevTimestamp;
        public bool   Primed;
        public int    ConsecutiveMisses;

        public ulong  TotalBytesRead;
        public ulong  TotalBytesWritten;
        public double ReadBytesPerSecond;
        public double WriteBytesPerSecond;
        public double PercentActiveTime;
    }

    private readonly Dictionary<string, DiskDeviceState> diskStates = new();

    private void OnStartDiskMetrics() => diskStates.Clear();

    private void OnStopDiskMetrics() => diskStates.Clear();

    private void OnDoWorkDiskMetrics(DiskInfo diskInfo)
    {
        UpdateDiskStates();
        BuildDiskMetrics(diskInfo.Metrics);
    }

    private void UpdateDiskStates()
    {
        if (IOKit.IOServiceGetMatchingServices(0, IOKit.IOServiceMatching(BlockStorageDriverClass), out IntPtr iterator) != 0 ||
            iterator == IntPtr.Zero) {
            return;
        }

        HashSet<string> live = new();
        uint entry;

        while ((entry = IOKit.IOIteratorNext(iterator)) != 0) {
            try {
                ReadDriverStatistics(entry, live);
            }
            finally {
                IOKit.IOObjectRelease(entry);
            }
        }

        IOKit.IOObjectRelease(iterator);
        PruneMissingDisks(live);
    }

    private void ReadDriverStatistics(uint entry, HashSet<string> live)
    {
        string? bsdName = SearchStringProperty(entry, "BSD Name", IterateRecursively);
        int index = ParseDiskIndex(bsdName);

        if (index < 0 || bsdName == null) {
            return;
        }

        if (IOKit.IORegistryEntryCreateCFProperties(entry, out IntPtr properties, IntPtr.Zero, 0) != 0 ||
            properties == IntPtr.Zero) {
            return;
        }

        Dictionary<string, nint> props = CoreFoundation.ToDictionary(properties);

        if (!props.TryGetValue("Statistics", out nint statsRef)) {
            CoreFoundation.CFRelease(properties);
            return;
        }

        // Values below are borrowed from the retained 'properties'; read them before releasing it.
        Dictionary<string, nint> stats = CoreFoundation.ToDictionary(statsRef);

        long readBytes  = ReadStat(stats, "Bytes (Read)");
        long writeBytes = ReadStat(stats, "Bytes (Write)");
        long readTime   = ReadStat(stats, "Total Time (Read)");
        long writeTime  = ReadStat(stats, "Total Time (Write)");

        CoreFoundation.CFRelease(properties);

        live.Add(bsdName);
        ApplyDiskSample(bsdName, index, readBytes, writeBytes, readTime, writeTime);
    }

    private static long ReadStat(Dictionary<string, nint> stats, string key)
    {
        if (stats.TryGetValue(key, out nint number)) {
            CoreFoundation.CFNumberGetValue(number, out long value);
            return value;
        }

        return 0;
    }

    private void ApplyDiskSample(
        string bsdName,
        int index,
        long readBytes,
        long writeBytes,
        long readTime,
        long writeTime)
    {
        if (!diskStates.TryGetValue(bsdName, out DiskDeviceState? state)) {
            state = new DiskDeviceState();
            diskStates[bsdName] = state;
        }

        state.Index = index;
        long now = Stopwatch.GetTimestamp();

        if (!state.Primed) {
            RebaseDisk(state, readBytes, writeBytes, readTime, writeTime, now);
            state.Primed = true;
            return;
        }

        double elapsedSeconds = (now - state.PrevTimestamp) / (double)Stopwatch.Frequency;

        if (elapsedSeconds <= 0.0) {
            return;
        }

        long readDelta      = readBytes - state.PrevReadBytes;
        long writeDelta     = writeBytes - state.PrevWriteBytes;
        long readTimeDelta  = readTime - state.PrevReadTimeNanos;
        long writeTimeDelta = writeTime - state.PrevWriteTimeNanos;

        state.ReadBytesPerSecond = readDelta > 0 ? readDelta / elapsedSeconds : 0.0;
        state.WriteBytesPerSecond = writeDelta > 0 ? writeDelta / elapsedSeconds : 0.0;

        if (readDelta > 0) {
            state.TotalBytesRead += (ulong)readDelta;
        }

        if (writeDelta > 0) {
            state.TotalBytesWritten += (ulong)writeDelta;
        }

        // Active time is the fraction of the interval the device spent servicing I/O, from the
        // cumulative nanosecond latency counters.
        double busyNanos = (readTimeDelta > 0 ? readTimeDelta : 0) + (writeTimeDelta > 0 ? writeTimeDelta : 0);
        double elapsedNanos = elapsedSeconds * NanosecondsPerSecond;

        state.PercentActiveTime = elapsedNanos > 0.0
            ? Math.Clamp(100.0 * busyNanos / elapsedNanos, 0.0, 100.0)
            : 0.0;

        RebaseDisk(state, readBytes, writeBytes, readTime, writeTime, now);
    }

    private static void RebaseDisk(
        DiskDeviceState state,
        long readBytes,
        long writeBytes,
        long readTime,
        long writeTime,
        long now)
    {
        state.PrevReadBytes      = readBytes;
        state.PrevWriteBytes     = writeBytes;
        state.PrevReadTimeNanos  = readTime;
        state.PrevWriteTimeNanos = writeTime;
        state.PrevTimestamp      = now;
    }

    private void PruneMissingDisks(HashSet<string> live)
    {
        const int MaxMisses = 3;
        List<string>? stale = null;

        foreach ((string name, DiskDeviceState state) in diskStates) {
            if (live.Contains(name)) {
                state.ConsecutiveMisses = 0;
                continue;
            }

            if (++state.ConsecutiveMisses >= MaxMisses) {
                (stale ??= new()).Add(name);
            }
        }

        if (stale == null) {
            return;
        }

        foreach (string name in stale) {
            diskStates.Remove(name);
        }
    }

    private void BuildDiskMetrics(DiskMetrics metrics)
    {
        ulong totalRead = 0;
        ulong totalWritten = 0;
        double totalReadPerSec = 0.0;
        double totalWritePerSec = 0.0;
        double busiest = 0.0;

        foreach ((string name, DiskDeviceState state) in diskStates) {
            DiskDeviceMetrics device = new();
            // Not inline declared to assist with debugging.
            device.Index                   = state.Index;
            device.InstanceName            = name;
            device.TotalBytesRead          = state.TotalBytesRead;
            device.TotalBytesWritten       = state.TotalBytesWritten;
            device.ReadBytesPerSecond      = state.ReadBytesPerSecond;
            device.WriteBytesPerSecond     = state.WriteBytesPerSecond;
            device.ReadMegabytesPerSecond  = state.ReadBytesPerSecond / BytesPerMegabyte;
            device.WriteMegabytesPerSecond = state.WriteBytesPerSecond / BytesPerMegabyte;
            device.PercentActiveTime       = state.PercentActiveTime;

            metrics.Devices.Add(device);

            totalRead += state.TotalBytesRead;
            totalWritten += state.TotalBytesWritten;
            totalReadPerSec += state.ReadBytesPerSecond;
            totalWritePerSec += state.WriteBytesPerSecond;
            busiest = Math.Max(busiest, state.PercentActiveTime);
        }

        metrics.Devices.Sort((left, right) => left.Index.CompareTo(right.Index));

        metrics.TotalBytesRead = totalRead;
        metrics.TotalBytesWritten = totalWritten;
        metrics.ReadBytesPerSecond = totalReadPerSec;
        metrics.WriteBytesPerSecond = totalWritePerSec;
        metrics.ReadMegabytesPerSecond = totalReadPerSec / BytesPerMegabyte;
        metrics.WriteMegabytesPerSecond = totalWritePerSec / BytesPerMegabyte;
        metrics.PercentActiveTime = busiest;
    }
}
#endif
