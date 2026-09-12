using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Disk;

public partial class DiskService
{
#if __WIN32__
    // The raw value behind these two counters is cumulative bytes since boot rather than a rate,
    // which is what makes an exact running total possible. The rate is then derived from the same
    // delta over the interval the counter itself timestamps, rather than over a nominal Delay that
    // ignores how long the cycle actually took. That needs a query which stays open across ticks.
    private const string DiskReadBytesCounterPath  = @"\PhysicalDisk(*)\Disk Read Bytes/sec";
    private const string DiskWriteBytesCounterPath = @"\PhysicalDisk(*)\Disk Write Bytes/sec";

    // Task Manager's "Active time" is the inverse of idle. "% Disk Time" is a different number:
    // it is derived from queue length and reads well above 100% on a multi queue NVMe device.
    private const string DiskIdleTimeCounterPath   = @"\PhysicalDisk(*)\% Idle Time";

    private const string TotalInstanceName = "_Total";
    private const uint   PDH_CSTATUS_NEW_DATA = 0x00000001;

    // 1024 based, matching how Task Manager formats its KB/s readouts.
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

        // Successful collections in a row that did not list this instance. An eject is permanent;
        // a single miss is usually the provider rebuilding its instance list.
        public int ConsecutiveMisses;

        public ulong  TotalBytesRead;
        public ulong  TotalBytesWritten;
        public double ReadBytesPerSecond;
        public double WriteBytesPerSecond;
        public double PercentActiveTime;
    }

    private readonly struct RawSample
    {
        public RawSample(long value, long timeStamp)
        {
            Value = value;
            TimeStamp = timeStamp;
        }

        public long Value { get; }
        public long TimeStamp { get; }
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

        // The wildcard is re-expanded on every PdhCollectQueryData, so a disk attached after this
        // point is still picked up.
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
        uint pdhResult = Pdh.PdhAddEnglishCounter(query, counterPath, nint.Zero, &handle);

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

        // The sampling and the publishing are deliberately separate. PdhCollectQueryData returns
        // PDH_NO_DATA now and again while the provider rebuilds its instance list, and a cycle
        // that gave up at that point would publish an empty DiskMetrics: the device list would
        // vanish and the cumulative totals would collapse to zero and recover a cycle later.
        // instanceStates holds the last good sample, so a failed cycle republishes it untouched
        // and only skips the update. The next successful cycle spans two intervals and still
        // reports the right rate, because the interval comes from the counter's own timestamps
        // rather than from a nominal Delay.
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

        // One buffer serves all three reads: each copies its values into managed state before the
        // next overwrites it.
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

        // Forget instances the provider has stopped listing, e.g. an ejected removable drive, so
        // BuildMetrics stops republishing a device that is no longer there. Guarded on a non-empty
        // collection: a cycle that read nothing at all is a transient provider hiccup handled by
        // the early returns above, not every disk disappearing at once.
        if (reads.Count > 0) {
            PruneMissingInstances(reads);
        }
    }

    // The provider intermittently returns a successful, non-empty collection that omits a disk
    // that is still plugged in, then lists it again the next cycle. Dropping it on the first miss
    // makes the panel flicker, so an instance is only forgotten once it has been absent from
    // several collections in a row.
    private const int MissesBeforeInstanceForgotten = 3;

    private void PruneMissingInstances(Dictionary<string, RawSample> liveInstances)
    {
        List<string>? stale = null;

        foreach ((string instanceName, DiskInstanceState state) in instanceStates) {
            if (liveInstances.ContainsKey(instanceName)) {
                state.ConsecutiveMisses = 0;
                continue;
            }

            if (++state.ConsecutiveMisses >= MissesBeforeInstanceForgotten) {
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

        // The first sighting of an instance only records a baseline, since a delta needs two
        // samples.
        if (!state.Primed) {
            state.PreviousReadBytes = read.Value;
            state.PreviousWriteBytes = write.Value;
            state.PreviousIdleTime = idleTime.Value;
            state.PreviousTimeStamp = read.TimeStamp;
            state.Primed = true;

            return;
        }

        // A timestamp that has not advanced means the provider has not refreshed since the last
        // cycle, so there is no interval to divide by. The baseline is deliberately left alone:
        // moving it up to the current byte counts while keeping the old timestamp would absorb
        // every byte transferred since the last refresh into the baseline, where no later delta
        // can ever see them. The previous rates stand until the provider does refresh.
        if (elapsedTicks <= 0) {
            return;
        }

        long readDelta = read.Value - state.PreviousReadBytes;
        long writeDelta = write.Value - state.PreviousWriteBytes;
        long idleDelta = idleTime.Value - state.PreviousIdleTime;
        double elapsedSeconds = elapsedTicks / FileTimeTicksPerSecond;

        // Totals accumulate deltas rather than tracking an absolute baseline, so a counter that
        // resets underneath us costs one cycle instead of corrupting the running total.
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

        // Both idle time and the timestamp are in 100ns units, so the ratio is the fraction of the
        // interval the disk spent doing nothing.
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

            // Busiest single disk rather than the sum: two disks at 50% is a machine at 50%.
            busiestDisk = Math.Max(busiestDisk, state.PercentActiveTime);
        }

        metrics.Devices.Sort((left, right) => left.Index.CompareTo(right.Index));
        metrics.PercentActiveTime = busiestDisk;

        // Pdh maintains "_Total" itself, so it is preferred over the sum assembled above. The sum
        // is the fallback for a machine whose provider does not publish the total instance.
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

        // PDH_RAW_COUNTER_ITEM carries an LPWSTR, so it is not blittable and the buffer cannot be
        // cast to a pointer the way a formatted counter array can.
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

            samples[raw.szName] = new RawSample(raw.RawValue.FirstValue, raw.RawValue.TimeStamp.ToLong());
        }

        return true;
    }

    private unsafe bool TryGetRawCounterArray(nint counter, string counterPath, out uint itemCount)
    {
        itemCount = 0;

        // Pdh sizes the buffer for us. Instances come and go between ticks, so a buffer that was
        // large enough last cycle can fall short on this one; the buffer is grown and kept rather
        // than reallocated every cycle, and is shared by all three counters.
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
