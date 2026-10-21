using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Services.Common;

namespace Task.Monitor.System.Services.Disk;

public partial class DiskService
{
#if __WIN32__
    private const string DiskReadBytesCounterPath  = @"\PhysicalDisk(*)\Disk Read Bytes/sec";
    private const string DiskWriteBytesCounterPath = @"\PhysicalDisk(*)\Disk Write Bytes/sec";
    private const string DiskIdleTimeCounterPath   = @"\PhysicalDisk(*)\% Idle Time";

    private const double BytesPerMegabyte = 1024.0 * 1024.0;

    private PdhQuery? diskQuery;

    private nint readBytesCounter;
    private nint writeBytesCounter;
    private nint idleTimeCounter;

    private readonly Dictionary<string, DiskInstanceState> instanceStates = new();

    private sealed class DiskInstanceState : IMissedSampleCounter
    {
        public long PreviousReadBytes;
        public long PreviousWriteBytes;
        public long PreviousIdleTime;
        public long PreviousTimeStamp;
        public bool Primed;

        public int ConsecutiveMisses { get; set; }

        public ulong  TotalBytesRead;
        public ulong  TotalBytesWritten;
        public double ReadBytesPerSecond;
        public double WriteBytesPerSecond;
        public double PercentActiveTime;
    }

    private readonly struct RawSample(long value, long timeStamp)
    {
        public long Value     { get; } = value;
        public long TimeStamp { get; } = timeStamp;
    }

    private void BuildMetrics(DiskMetrics metrics)
    {
        ulong  summedBytesRead           = 0;
        ulong  summedBytesWritten        = 0;
        double summedReadBytesPerSecond  = 0.0;
        double summedWriteBytesPerSecond = 0.0;
        double busiestDisk               = 0.0;

        foreach ((string instanceName, DiskInstanceState state) in instanceStates) {
            if (instanceName == Pdh.TotalInstanceName) {
                continue;
            }

            int index = ParseDiskIndexFromInstance(instanceName);

            if (index < 0) {
                continue;
            }

            DiskDeviceMetrics deviceMetrics = new();
            // Not inline declared to assist with debugging.
            deviceMetrics.Index                   = index;
            deviceMetrics.InstanceName            = instanceName;
            deviceMetrics.TotalBytesRead          = state.TotalBytesRead;
            deviceMetrics.TotalBytesWritten       = state.TotalBytesWritten;
            deviceMetrics.ReadBytesPerSecond      = state.ReadBytesPerSecond;
            deviceMetrics.WriteBytesPerSecond     = state.WriteBytesPerSecond;
            deviceMetrics.ReadMegabytesPerSecond  = state.ReadBytesPerSecond / BytesPerMegabyte;
            deviceMetrics.WriteMegabytesPerSecond = state.WriteBytesPerSecond / BytesPerMegabyte;
            deviceMetrics.PercentActiveTime       = state.PercentActiveTime;

            metrics.Devices.Add(deviceMetrics);

            summedBytesRead           += state.TotalBytesRead;
            summedBytesWritten        += state.TotalBytesWritten;
            summedReadBytesPerSecond  += state.ReadBytesPerSecond;
            summedWriteBytesPerSecond += state.WriteBytesPerSecond;

            // Busiest single disk rather than the sum.
            busiestDisk = Math.Max(busiestDisk, state.PercentActiveTime);
        }

        metrics.Devices.Sort((left, right) => left.Index.CompareTo(right.Index));
        metrics.PercentActiveTime = busiestDisk;

        // Prefer the Pdh _Total counter over the summed stats.
        if (instanceStates.TryGetValue(Pdh.TotalInstanceName, out DiskInstanceState? total)) {
            metrics.TotalBytesRead      = total.TotalBytesRead;
            metrics.TotalBytesWritten   = total.TotalBytesWritten;
            metrics.ReadBytesPerSecond  = total.ReadBytesPerSecond;
            metrics.WriteBytesPerSecond = total.WriteBytesPerSecond;
        }
        else {
            metrics.TotalBytesRead      = summedBytesRead;
            metrics.TotalBytesWritten   = summedBytesWritten;
            metrics.ReadBytesPerSecond  = summedReadBytesPerSecond;
            metrics.WriteBytesPerSecond = summedWriteBytesPerSecond;
        }

        metrics.ReadMegabytesPerSecond  = metrics.ReadBytesPerSecond / BytesPerMegabyte;
        metrics.WriteMegabytesPerSecond = metrics.WriteBytesPerSecond / BytesPerMegabyte;
    }

    private void OnDoWorkDiskMetrics(DiskInfo diskInfo)
    {
        if (diskQuery is null) {
            return;
        }

        UpdateInstanceStates();
        BuildMetrics(diskInfo.Metrics);
    }

    private void OnStartDiskMetrics()
    {
        diskQuery = PdhQuery.Open(
            nameof(DiskService),
            DiskReadBytesCounterPath,
            DiskWriteBytesCounterPath,
            DiskIdleTimeCounterPath);

        if (diskQuery is not null) {
            readBytesCounter  = diskQuery.Counters[0];
            writeBytesCounter = diskQuery.Counters[1];
            idleTimeCounter   = diskQuery.Counters[2];
        }

        instanceStates.Clear();
    }

    private void OnStopDiskMetrics()
    {
        diskQuery?.Dispose();
        diskQuery = null;

        instanceStates.Clear();
    }

    private static int ParseDiskIndexFromInstance(string instanceName)
    {
        // Format example: "0 C:" or "1 D: E:", and just "2" for a disk carrying no mounted volume.
        // The leading integer is the physical drive number that \\.\PhysicalDriveN and
        // IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS both use, which is how a counter row is matched
        // back to a DiskDevice.
        int separatorIndex = instanceName.IndexOf(' ');

        ReadOnlySpan<char> indexSpan = separatorIndex == -1
            ? instanceName.AsSpan()
            : instanceName.AsSpan(0, separatorIndex);

        return int.TryParse(indexSpan, out int index) 
            ? index 
            : -1;
    }

    private bool TryReadRawCounterArray(nint counter, Dictionary<string, RawSample> samples)
    {
        if (diskQuery is null || !diskQuery.TryReadRaw(counter, out uint itemCount)) {
            return false;
        }

        int itemSize = Marshal.SizeOf<Pdh.PDH_RAW_COUNTER_ITEM>();

        for (uint item = 0; item < itemCount; item++) {
            nint itemPointer             = diskQuery.Buffer + (nint)(item * (uint)itemSize);
            Pdh.PDH_RAW_COUNTER_ITEM raw = Marshal.PtrToStructure<Pdh.PDH_RAW_COUNTER_ITEM>(itemPointer);

            if (raw.RawValue.CStatus != Pdh.PDH_CSTATUS_VALID_DATA &&
                raw.RawValue.CStatus != Pdh.PDH_CSTATUS_NEW_DATA) {

                continue;
            }

            if (string.IsNullOrEmpty(raw.szName)) {
                continue;
            }

            samples[raw.szName] = new RawSample(
                raw.RawValue.FirstValue, 
                raw.RawValue.TimeStamp.ToLong());
        }

        return true;
    }

    private void UpdateInstanceState(
        string instanceName,
        RawSample read,
        RawSample write,
        RawSample idleTime)
    {
        if (!instanceStates.TryGetValue(instanceName, out DiskInstanceState? state)) {
            state = new DiskInstanceState();
            instanceStates[instanceName] = state;
        }

        long elapsedTicks = read.TimeStamp - state.PreviousTimeStamp;

        if (!state.Primed) {
            state.PreviousReadBytes  = read.Value;
            state.PreviousWriteBytes = write.Value;
            state.PreviousIdleTime   = idleTime.Value;
            state.PreviousTimeStamp  = read.TimeStamp;
            state.Primed             = true;

            return;
        }

        if (elapsedTicks <= 0) {
            return;
        }

        double elapsedSeconds = elapsedTicks / CounterRate.FileTimeTicksPerSecond;
        long   idleDelta      = idleTime.Value - state.PreviousIdleTime;

        state.ReadBytesPerSecond = CounterRate.Accumulate(
            read.Value,
            ref state.PreviousReadBytes,
            ref state.TotalBytesRead,
            elapsedSeconds);

        state.WriteBytesPerSecond = CounterRate.Accumulate(
            write.Value,
            ref state.PreviousWriteBytes,
            ref state.TotalBytesWritten,
            elapsedSeconds);

        state.PercentActiveTime = idleDelta >= 0
            ? Math.Clamp(100.0 * (1.0 - (idleDelta / (double)elapsedTicks)), 0.0, 100.0)
            : 0.0;

        state.PreviousIdleTime  = idleTime.Value;
        state.PreviousTimeStamp = read.TimeStamp;
    }

    private void UpdateInstanceStates()
    {
        if (diskQuery is null || !diskQuery.Collect()) {
            return;
        }

        Dictionary<string, RawSample> reads = new();
        Dictionary<string, RawSample> writes = new();
        Dictionary<string, RawSample> idleTimes = new();

        if (!TryReadRawCounterArray(readBytesCounter, reads) ||
            !TryReadRawCounterArray(writeBytesCounter, writes) ||
            !TryReadRawCounterArray(idleTimeCounter, idleTimes)) {

            return;
        }

        foreach ((string instanceName, RawSample read) in reads) {
            if (!writes.TryGetValue(instanceName, out RawSample write) ||
                !idleTimes.TryGetValue(instanceName, out RawSample idleTime)) {

                continue;
            }

            UpdateInstanceState(instanceName, read, write, idleTime);
        }

        if (reads.Count > 0) {
            PruneMissing(instanceStates, reads.ContainsKey);
        }
    }
#endif
}
