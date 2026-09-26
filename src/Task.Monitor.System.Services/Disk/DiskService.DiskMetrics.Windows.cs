using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Disk;

public partial class DiskService
{
#if __WIN32__
    private const string DiskReadBytesCounterPath  = @"\PhysicalDisk(*)\Disk Read Bytes/sec";
    private const string DiskWriteBytesCounterPath = @"\PhysicalDisk(*)\Disk Write Bytes/sec";
    private const string DiskIdleTimeCounterPath   = @"\PhysicalDisk(*)\% Idle Time";

    private const string TotalInstanceName = "_Total";
    private const uint   PDH_CSTATUS_NEW_DATA = 0x00000001;

    private const double BytesPerMegabyte = 1024.0 * 1024.0;
    private const double FileTimeTicksPerSecond = 10_000_000.0;

    private nint diskQuery = nint.Zero;
    private nint readBytesCounter = nint.Zero;
    private nint writeBytesCounter = nint.Zero;
    private nint idleTimeCounter = nint.Zero;
    private nint counterBuffer = nint.Zero;
    private uint counterBufferSize = 0;

    private readonly Dictionary<string, DiskInstanceState> instanceStates = new();

    private sealed class DiskInstanceState
    {
        public long PreviousReadBytes;
        public long PreviousWriteBytes;
        public long PreviousIdleTime;
        public long PreviousTimeStamp;
        public bool Primed;

        public int ConsecutiveMisses;

        public ulong  TotalBytesRead;
        public ulong  TotalBytesWritten;
        public double ReadBytesPerSecond;
        public double WriteBytesPerSecond;
        public double PercentActiveTime;
    }

    private readonly struct RawSample(long value, long timeStamp)
    {
        public long Value { get; } = value;
        public long TimeStamp { get; } = timeStamp;
    }

    private unsafe void OnStartDiskMetrics()
    {
        uint pdhResult;
        nint query = nint.Zero;

        if ((pdhResult = Pdh.PdhOpenQuery(
            null,
            nint.Zero,
            &query)) != Pdh.ERROR_SUCCESS) {

            PInvokeErrorHelpers.TraceOnceOnPInvokeError(
                nameof(Pdh.PdhOpenQuery),
                $"Failed {nameof(OnStartDiskMetrics)}",
                pdhResult);

            return;
        }

        if (!TryAddCounter(query, DiskReadBytesCounterPath, out nint readCounter) ||
            !TryAddCounter(query, DiskWriteBytesCounterPath, out nint writeCounter) ||
            !TryAddCounter(query, DiskIdleTimeCounterPath, out nint idleCounter)) {

            Pdh.PdhCloseQuery(query);
            return;
        }

        diskQuery = query;
        readBytesCounter = readCounter;
        writeBytesCounter = writeCounter;
        idleTimeCounter = idleCounter;

        instanceStates.Clear();
    }

    private static unsafe bool TryAddCounter(nint query, string counterPath, out nint counter)
    {
        nint handle = nint.Zero;
        
        uint pdhResult = Pdh.PdhAddEnglishCounter(
            query, 
            counterPath, 
            nint.Zero, 
            &handle);

        counter = handle;

        if (pdhResult == Pdh.ERROR_SUCCESS) {
            return true;
        }

        PInvokeErrorHelpers.TraceOnceOnPInvokeError(
            counterPath,
            $"Failed {nameof(Pdh.PdhAddEnglishCounter)}",
            pdhResult);

        return false;
    }

    private void OnStopDiskMetrics()
    {
        if (counterBuffer != nint.Zero) {
            Marshal.FreeHGlobal(counterBuffer);
            counterBuffer = nint.Zero;
            counterBufferSize = 0;
        }

        if (diskQuery != nint.Zero) {
            Pdh.PdhCloseQuery(diskQuery);
            diskQuery = nint.Zero;
            readBytesCounter = nint.Zero;
            writeBytesCounter = nint.Zero;
            idleTimeCounter = nint.Zero;
        }

        instanceStates.Clear();
    }

    private void OnDoWorkDiskMetrics(DiskInfo diskInfo)
    {
        if (diskQuery == nint.Zero) {
            return;
        }
        
        UpdateInstanceStates();
        BuildMetrics(diskInfo.Metrics);
    }

    private void UpdateInstanceStates()
    {
        uint pdhResult;

        if ((pdhResult = Pdh.PdhCollectQueryData(diskQuery)) != Pdh.ERROR_SUCCESS) {
            PInvokeErrorHelpers.TraceOnceOnPInvokeError(
                nameof(Pdh.PdhCollectQueryData),
                $"Failed {nameof(OnDoWorkDiskMetrics)}",
                pdhResult);

            return;
        }

        Dictionary<string, RawSample> reads = new();
        Dictionary<string, RawSample> writes = new();
        Dictionary<string, RawSample> idleTimes = new();

        if (!TryReadRawCounterArray(readBytesCounter, DiskReadBytesCounterPath, reads) ||
            !TryReadRawCounterArray(writeBytesCounter, DiskWriteBytesCounterPath, writes) ||
            !TryReadRawCounterArray(idleTimeCounter, DiskIdleTimeCounterPath, idleTimes)) {

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
            PruneMissingInstances(reads);
        }
    }

    private void PruneMissingInstances(Dictionary<string, RawSample> liveInstances)
    {
        const int MaxMisses = 3;
        List<string>? stale = null;

        foreach ((string instanceName, DiskInstanceState state) in instanceStates) {
            if (liveInstances.ContainsKey(instanceName)) {
                state.ConsecutiveMisses = 0;
                continue;
            }

            if (++state.ConsecutiveMisses >= MaxMisses) {
                (stale ??= new()).Add(instanceName);
            }
        }

        if (stale == null) {
            return;
        }

        foreach (string instanceName in stale) {
            instanceStates.Remove(instanceName);
        }
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
            state.PreviousReadBytes = read.Value;
            state.PreviousWriteBytes = write.Value;
            state.PreviousIdleTime = idleTime.Value;
            state.PreviousTimeStamp = read.TimeStamp;
            state.Primed = true;

            return;
        }

        if (elapsedTicks <= 0) {
            return;
        }

        long readDelta = read.Value - state.PreviousReadBytes;
        long writeDelta = write.Value - state.PreviousWriteBytes;
        long idleDelta = idleTime.Value - state.PreviousIdleTime;
        double elapsedSeconds = elapsedTicks / FileTimeTicksPerSecond;

        if (readDelta > 0) {
            state.TotalBytesRead += (ulong)readDelta;
            state.ReadBytesPerSecond = readDelta / elapsedSeconds;
        }
        else {
            state.ReadBytesPerSecond = 0.0;
        }

        if (writeDelta > 0) {
            state.TotalBytesWritten += (ulong)writeDelta;
            state.WriteBytesPerSecond = writeDelta / elapsedSeconds;
        }
        else {
            state.WriteBytesPerSecond = 0.0;
        }

        state.PercentActiveTime = idleDelta >= 0
            ? Math.Clamp(100.0 * (1.0 - (idleDelta / (double)elapsedTicks)), 0.0, 100.0)
            : 0.0;

        state.PreviousReadBytes = read.Value;
        state.PreviousWriteBytes = write.Value;
        state.PreviousIdleTime = idleTime.Value;
        state.PreviousTimeStamp = read.TimeStamp;
    }

    private void BuildMetrics(DiskMetrics metrics)
    {
        ulong summedBytesRead = 0;
        ulong summedBytesWritten = 0;
        double summedReadBytesPerSecond = 0.0;
        double summedWriteBytesPerSecond = 0.0;
        double busiestDisk = 0.0;

        foreach ((string instanceName, DiskInstanceState state) in instanceStates) {
            if (instanceName == TotalInstanceName) {
                continue;
            }

            int index = ParseDiskIndexFromInstance(instanceName);

            if (index < 0) {
                continue;
            }

            DiskDeviceMetrics deviceMetrics = new();
            // Not inline declared to assist with debugging.
            deviceMetrics.Index = index;
            deviceMetrics.InstanceName = instanceName;
            deviceMetrics.TotalBytesRead = state.TotalBytesRead;
            deviceMetrics.TotalBytesWritten = state.TotalBytesWritten;
            deviceMetrics.ReadBytesPerSecond = state.ReadBytesPerSecond;
            deviceMetrics.WriteBytesPerSecond = state.WriteBytesPerSecond;
            deviceMetrics.ReadMegabytesPerSecond = state.ReadBytesPerSecond / BytesPerMegabyte;
            deviceMetrics.WriteMegabytesPerSecond = state.WriteBytesPerSecond / BytesPerMegabyte;
            deviceMetrics.PercentActiveTime = state.PercentActiveTime;

            metrics.Devices.Add(deviceMetrics);

            summedBytesRead += state.TotalBytesRead;
            summedBytesWritten += state.TotalBytesWritten;
            summedReadBytesPerSecond += state.ReadBytesPerSecond;
            summedWriteBytesPerSecond += state.WriteBytesPerSecond;

            // Busiest single disk rather than the sum.
            busiestDisk = Math.Max(busiestDisk, state.PercentActiveTime);
        }

        metrics.Devices.Sort((left, right) => left.Index.CompareTo(right.Index));
        metrics.PercentActiveTime = busiestDisk;

        // Prefer the Pdh _Total counter over the summed stats.
        if (instanceStates.TryGetValue(TotalInstanceName, out DiskInstanceState? total)) {
            metrics.TotalBytesRead = total.TotalBytesRead;
            metrics.TotalBytesWritten = total.TotalBytesWritten;
            metrics.ReadBytesPerSecond = total.ReadBytesPerSecond;
            metrics.WriteBytesPerSecond = total.WriteBytesPerSecond;
        }
        else {
            metrics.TotalBytesRead = summedBytesRead;
            metrics.TotalBytesWritten = summedBytesWritten;
            metrics.ReadBytesPerSecond = summedReadBytesPerSecond;
            metrics.WriteBytesPerSecond = summedWriteBytesPerSecond;
        }

        metrics.ReadMegabytesPerSecond = metrics.ReadBytesPerSecond / BytesPerMegabyte;
        metrics.WriteMegabytesPerSecond = metrics.WriteBytesPerSecond / BytesPerMegabyte;
    }

    private unsafe bool TryReadRawCounterArray(
        nint counter,
        string counterPath,
        Dictionary<string, RawSample> samples)
    {
        if (!TryGetRawCounterArray(counter, counterPath, out uint itemCount)) {
            return false;
        }

        int itemSize = Marshal.SizeOf<Pdh.PDH_RAW_COUNTER_ITEM>();

        for (uint item = 0; item < itemCount; item++) {
            nint itemPointer = counterBuffer + (nint)(item * (uint)itemSize);
            Pdh.PDH_RAW_COUNTER_ITEM raw = Marshal.PtrToStructure<Pdh.PDH_RAW_COUNTER_ITEM>(itemPointer);

            if (raw.RawValue.CStatus != Pdh.PDH_CSTATUS_VALID_DATA &&
                raw.RawValue.CStatus != PDH_CSTATUS_NEW_DATA) {

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

    private unsafe bool TryGetRawCounterArray(nint counter, string counterPath, out uint itemCount)
    {
        itemCount = 0;

        for (int attempt = 0; attempt < 3; attempt++) {
            uint bufferSize = counterBufferSize;
            uint count = 0;

            uint result = Pdh.PdhGetRawCounterArray(
                counter,
                &bufferSize,
                &count,
                counterBuffer);

            if (result == Pdh.ERROR_SUCCESS) {
                itemCount = count;
                return true;
            }

            if (result != unchecked((uint)Pdh.PDH_MORE_DATA)) {
                PInvokeErrorHelpers.TraceOnceOnPInvokeError(
                    $"{nameof(Pdh.PdhGetRawCounterArray)} {counterPath}",
                    $"Failed {nameof(OnDoWorkDiskMetrics)}",
                    result);

                return false;
            }

            if (bufferSize <= counterBufferSize) {
                TraceEx.WriteLineOnce(
                    $"{nameof(Pdh.PdhGetRawCounterArray)} {counterPath}",
                    $"{nameof(Pdh.PdhGetRawCounterArray)} failed to calculate {nameof(bufferSize)}");

                return false;
            }

            if (counterBuffer != nint.Zero) {
                Marshal.FreeHGlobal(counterBuffer);
                counterBuffer = nint.Zero;
                counterBufferSize = 0;
            }

            counterBuffer = Marshal.AllocHGlobal((int)bufferSize);
            counterBufferSize = bufferSize;
        }

        return false;
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

        return int.TryParse(indexSpan, out int index) ? index : -1;
    }
#endif
}
