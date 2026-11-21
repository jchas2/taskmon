using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;
using Task.Monitor.System.Native;
using Task.Monitor.System.Services.Common;

namespace Task.Monitor.System.Services.Disk;

public partial class DiskService
{
#if __WIN32__
    private const string DiskReadBytesCounterPath  = @"\PhysicalDisk(*)\Disk Read Bytes/sec";
    private const string DiskWriteBytesCounterPath = @"\PhysicalDisk(*)\Disk Write Bytes/sec";
    private const string DiskIdleTimeCounterPath   = @"\PhysicalDisk(*)\% Idle Time";

    private const string DiskLatencyCounterPath     = @"\PhysicalDisk(*)\Avg. Disk sec/Transfer";
    private const string DiskQueueLengthCounterPath = @"\PhysicalDisk(*)\Current Disk Queue Length";

    private const double MillisecondsPerSecond = 1000.0;

    private PdhQuery? diskQuery;
    private PdhQuery? pressureQuery;

    private nint readBytesCounter;
    private nint writeBytesCounter;
    private nint idleTimeCounter;

    private readonly record struct RawSample(long Value, long TimeStamp);

    private readonly Dictionary<string, RawSample> reads     = new();
    private readonly Dictionary<string, RawSample> writes    = new();
    private readonly Dictionary<string, RawSample> idleTimes = new();

    private void OnDoWorkDiskMetrics(DiskInfo diskInfo)
    {
        if (diskQuery is null) {
            return;
        }

        UpdateInstanceStates();
        BuildMetrics(diskInfo.Metrics);

        if (states.TryGetValue(Pdh.TotalInstanceName, out DiskState? total)) {
            diskInfo.Metrics.TotalBytesRead      = total.TotalBytesRead;
            diskInfo.Metrics.TotalBytesWritten   = total.TotalBytesWritten;
            diskInfo.Metrics.ReadBytesPerSecond  = total.ReadBytesPerSecond;
            diskInfo.Metrics.WriteBytesPerSecond = total.WriteBytesPerSecond;
        }

        ReadPressureMetrics(diskInfo.Metrics);
    }

    partial void OnStartDiskMetrics()
    {
        pressureQuery = PdhQuery.OpenAtStartup(ServiceName, "pressure counters", DiskLatencyCounterPath, DiskQueueLengthCounterPath);
        pressureQuery?.Collect();

        diskQuery = PdhQuery.OpenAtStartup(
            ServiceName,
            "disk counters",
            DiskReadBytesCounterPath,
            DiskWriteBytesCounterPath,
            DiskIdleTimeCounterPath);

        if (diskQuery is not null) {
            readBytesCounter  = diskQuery.Counters[0];
            writeBytesCounter = diskQuery.Counters[1];
            idleTimeCounter   = diskQuery.Counters[2];
        }
    }

    private void OnStopDiskMetrics()
    {
        diskQuery?.Dispose();
        diskQuery = null;

        pressureQuery?.Dispose();
        pressureQuery = null;
    }

    private static int ParseDiskIndexFromInstance(string instanceName)
    {
        // Format example: "0 C:" or "1 D: E:", and just "2" for a disk carrying no mounted volume.
        int separatorIndex = instanceName.IndexOf(' ');

        ReadOnlySpan<char> indexSpan = separatorIndex == -1
            ? instanceName.AsSpan()
            : instanceName.AsSpan(0, separatorIndex);

        return int.TryParse(indexSpan, out int index) 
            ? index 
            : -1;
    }

    private unsafe Dictionary<string, double> ReadInstanceValues(nint counter)
    {
        Dictionary<string, double> values = new();

        if (pressureQuery is null || !pressureQuery.TryReadFormattedDoubles(counter, out uint itemCount)) {
            return values;
        }

        Pdh.PDH_FMT_COUNTERVALUE_ITEM_W* items = (Pdh.PDH_FMT_COUNTERVALUE_ITEM_W*)pressureQuery.Buffer;

        for (uint i = 0; i < itemCount; i++) {
            if (items[i].CStatus == Pdh.PDH_CSTATUS_VALID_DATA || items[i].CStatus == Pdh.PDH_CSTATUS_NEW_DATA) {
                values[Marshal.PtrToStringUni(items[i].szName) ?? string.Empty] = items[i].doubleValue;
            }
        }

        return values;
    }

    private void ReadPressureMetrics(DiskMetrics metrics)
    {
        if (pressureQuery is null || !pressureQuery.Collect()) {
            return;
        }

        Dictionary<string, double> latencies    = ReadInstanceValues(pressureQuery.Counters[0]);
        Dictionary<string, double> queueLengths = ReadInstanceValues(pressureQuery.Counters[1]);

        foreach (DiskDeviceMetrics device in metrics.Devices) {
            if (latencies.TryGetValue(device.InstanceName, out double seconds)) {
                device.LatencyMs = seconds * MillisecondsPerSecond;
                metrics.LatencyMs = Math.Max(metrics.LatencyMs ?? 0.0, device.LatencyMs.Value);
            }

            if (queueLengths.TryGetValue(device.InstanceName, out double queueLength)) {
                device.QueueLength  = queueLength;
                metrics.QueueLength = Math.Max(metrics.QueueLength ?? 0.0, queueLength);
            }
        }
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
        string    instanceName,
        RawSample read,
        RawSample write,
        RawSample idleTime)
    {
        if (!states.TryGetValue(instanceName, out DiskState? state)) {
            states[instanceName] = new DiskState {
                Index              = ParseDiskIndexFromInstance(instanceName),
                PreviousReadBytes  = read.Value,
                PreviousWriteBytes = write.Value,
                PreviousTime       = idleTime.Value,
                PreviousTimestamp  = read.TimeStamp
            };

            return;
        }

        long elapsedTicks = read.TimeStamp - state.PreviousTimestamp;

        if (elapsedTicks <= 0) {
            return;
        }

        double elapsedSeconds = elapsedTicks / CounterRate.FileTimeTicksPerSecond;
        long   idleDelta      = idleTime.Value - state.PreviousTime;

        state.ReadBytesPerSecond  = CounterRate.Accumulate(read.Value, ref state.PreviousReadBytes, ref state.TotalBytesRead, elapsedSeconds);
        state.WriteBytesPerSecond = CounterRate.Accumulate(write.Value, ref state.PreviousWriteBytes, ref state.TotalBytesWritten, elapsedSeconds);
        state.PercentActiveTime   = idleDelta >= 0
            ? Math.Clamp(100.0 * (1.0 - (idleDelta / (double)elapsedTicks)), 0.0, 100.0)
            : 0.0;
        state.PreviousTime        = idleTime.Value;
        state.PreviousTimestamp   = read.TimeStamp;
    }

    private void UpdateInstanceStates()
    {
        if (diskQuery is null || !diskQuery.Collect()) {
            return;
        }

        reads.Clear();
        writes.Clear();
        idleTimes.Clear();
        
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
            PruneMissing(reads.ContainsKey);
        }
    }
#endif
}
