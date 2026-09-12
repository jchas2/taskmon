using System.Buffers.Binary;
using System.Diagnostics;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Network;

public partial class NetworkService
{
#if __WIN32__
    // GetIfTable2 rather than the "\Network Interface(*)" Pdh counters. The rows are keyed by
    // InterfaceLuid, which joins cleanly to GetAdaptersAddresses, whereas a Pdh instance name is
    // the adapter description with its punctuation substituted and has to be matched by string.
    // It also avoids the wildcard instance expansion that intermittently returns PDH_NO_DATA.
    //
    // The cost is that GetIfTable2 carries no timestamp, so the interval is measured here.
    private const double BytesPerMegabyte = 1024.0 * 1024.0;

    // Adapters change far more slowly than the poll interval, so the list is re-enumerated on a
    // slow cadence. An adapter that appears mid run is picked up immediately instead, because the
    // sample sees a LUID the specs do not have.
    private const int SpecsRefreshCycles = 10;

    private readonly Dictionary<ulong, NetworkInstanceState> instanceStates = new();
    private long previousTimestamp;
    private bool primed;
    private int cyclesSinceSpecsRefresh;
    private bool sawUnknownAdapter;

    private sealed class NetworkInstanceState
    {
        public uint  InterfaceIndex;
        public ulong PreviousBytesSent;
        public ulong PreviousBytesReceived;
        public ulong PreviousPacketsSent;
        public ulong PreviousPacketsReceived;
        public bool  Primed;

        public ulong  TotalBytesSent;
        public ulong  TotalBytesReceived;
        public ulong  TotalPacketsSent;
        public ulong  TotalPacketsReceived;
        public double SendBytesPerSecond;
        public double ReceiveBytesPerSecond;
        public double SendPacketsPerSecond;
        public double ReceivePacketsPerSecond;
    }

    private readonly struct InterfaceSample
    {
        public InterfaceSample(
            uint interfaceIndex,
            ulong bytesSent,
            ulong bytesReceived,
            ulong packetsSent,
            ulong packetsReceived)
        {
            InterfaceIndex = interfaceIndex;
            BytesSent = bytesSent;
            BytesReceived = bytesReceived;
            PacketsSent = packetsSent;
            PacketsReceived = packetsReceived;
        }

        public uint  InterfaceIndex  { get; }
        public ulong BytesSent       { get; }
        public ulong BytesReceived   { get; }
        public ulong PacketsSent     { get; }
        public ulong PacketsReceived { get; }
    }

    private void OnStopNetworkMetrics()
    {
        instanceStates.Clear();
        previousTimestamp = 0;
        primed = false;
        cyclesSinceSpecsRefresh = 0;
        sawUnknownAdapter = false;
    }

    private void OnDoWorkNetworkSample()
    {
        Dictionary<ulong, InterfaceSample> samples = new();

        // A failed sample leaves the retained state untouched so the previous values are
        // republished, rather than blanking the metrics and collapsing the cumulative totals.
        if (!TryReadInterfaceTable(samples)) {
            return;
        }

        long timestamp = Stopwatch.GetTimestamp();
        long elapsedTicks = timestamp - previousTimestamp;

        if (!primed) {
            foreach ((ulong luid, InterfaceSample sample) in samples) {
                PrimeInstanceState(luid, sample);
            }

            previousTimestamp = timestamp;
            primed = true;

            return;
        }

        // No interval means nothing to divide by. The baseline is deliberately left alone: moving
        // it up while keeping the old timestamp would absorb the bytes transferred since the last
        // sample into the baseline, where no later delta can see them.
        if (elapsedTicks <= 0) {
            return;
        }

        double elapsedSeconds = elapsedTicks / (double)Stopwatch.Frequency;

        foreach ((ulong luid, InterfaceSample sample) in samples) {
            UpdateInstanceState(luid, sample, elapsedSeconds);
        }

        previousTimestamp = timestamp;
    }

    private void PrimeInstanceState(ulong interfaceLuid, InterfaceSample sample)
    {
        NetworkInstanceState state = new();
        state.InterfaceIndex = sample.InterfaceIndex;
        state.PreviousBytesSent = sample.BytesSent;
        state.PreviousBytesReceived = sample.BytesReceived;
        state.PreviousPacketsSent = sample.PacketsSent;
        state.PreviousPacketsReceived = sample.PacketsReceived;
        state.Primed = true;

        instanceStates[interfaceLuid] = state;
    }

    private void UpdateInstanceState(ulong interfaceLuid, InterfaceSample sample, double elapsedSeconds)
    {
        if (!instanceStates.TryGetValue(interfaceLuid, out NetworkInstanceState? state)) {
            // An adapter that was not there last cycle. Prime it and tell the service to
            // re-enumerate so it has a name and an address to go with these counters.
            PrimeInstanceState(interfaceLuid, sample);
            sawUnknownAdapter = true;

            return;
        }

        state.InterfaceIndex = sample.InterfaceIndex;

        // Totals accumulate deltas rather than tracking an absolute baseline, so a counter that
        // resets underneath us costs one cycle instead of corrupting the running total.
        state.SendBytesPerSecond = AccumulateDelta(
            sample.BytesSent, ref state.PreviousBytesSent, ref state.TotalBytesSent, elapsedSeconds);

        state.ReceiveBytesPerSecond = AccumulateDelta(
            sample.BytesReceived, ref state.PreviousBytesReceived, ref state.TotalBytesReceived, elapsedSeconds);

        state.SendPacketsPerSecond = AccumulateDelta(
            sample.PacketsSent, ref state.PreviousPacketsSent, ref state.TotalPacketsSent, elapsedSeconds);

        state.ReceivePacketsPerSecond = AccumulateDelta(
            sample.PacketsReceived, ref state.PreviousPacketsReceived, ref state.TotalPacketsReceived, elapsedSeconds);
    }

    private static double AccumulateDelta(
        ulong current,
        ref ulong previous,
        ref ulong total,
        double elapsedSeconds)
    {
        double rate = 0.0;

        if (current > previous) {
            ulong delta = current - previous;
            total += delta;
            rate = delta / elapsedSeconds;
        }

        previous = current;

        return rate;
    }

    private bool ShouldRefreshNetworkSpecs(NetworkSpecs specs)
    {
        if (sawUnknownAdapter) {
            sawUnknownAdapter = false;
            cyclesSinceSpecsRefresh = 0;

            return true;
        }

        if (++cyclesSinceSpecsRefresh < SpecsRefreshCycles) {
            return false;
        }

        cyclesSinceSpecsRefresh = 0;

        return true;
    }

    private void OnBuildNetworkMetrics(NetworkMetrics metrics, NetworkSpecs specs)
    {
        Dictionary<ulong, NetworkDevice> devices = new();

        foreach (NetworkDevice device in specs.Devices) {
            devices[device.InterfaceLuid] = device;
        }

        foreach ((ulong luid, NetworkInstanceState state) in instanceStates) {
            if (!devices.TryGetValue(luid, out NetworkDevice? device) || !device.IsActive) {
                continue;
            }

            NetworkDeviceMetrics deviceMetrics = new();
            // Not inline declared to assist with debugging.
            deviceMetrics.InterfaceIndex = device.InterfaceIndex;
            deviceMetrics.InterfaceLuid = luid;
            deviceMetrics.FriendlyName = device.FriendlyName;
            deviceMetrics.TotalBytesSent = state.TotalBytesSent;
            deviceMetrics.TotalBytesReceived = state.TotalBytesReceived;
            deviceMetrics.TotalPacketsSent = state.TotalPacketsSent;
            deviceMetrics.TotalPacketsReceived = state.TotalPacketsReceived;
            deviceMetrics.SendBytesPerSecond = state.SendBytesPerSecond;
            deviceMetrics.ReceiveBytesPerSecond = state.ReceiveBytesPerSecond;
            deviceMetrics.SendPacketsPerSecond = state.SendPacketsPerSecond;
            deviceMetrics.ReceivePacketsPerSecond = state.ReceivePacketsPerSecond;
            deviceMetrics.SendMegabytesPerSecond = state.SendBytesPerSecond / BytesPerMegabyte;
            deviceMetrics.ReceiveMegabytesPerSecond = state.ReceiveBytesPerSecond / BytesPerMegabyte;

            metrics.Devices.Add(deviceMetrics);

            // A tunnel reports the same bytes as the adapter it runs over, so it appears in the
            // per adapter list but is left out of the totals.
            if (!device.CountsTowardAggregate) {
                continue;
            }

            metrics.TotalBytesSent += state.TotalBytesSent;
            metrics.TotalBytesReceived += state.TotalBytesReceived;
            metrics.TotalPacketsSent += state.TotalPacketsSent;
            metrics.TotalPacketsReceived += state.TotalPacketsReceived;
            metrics.SendBytesPerSecond += state.SendBytesPerSecond;
            metrics.ReceiveBytesPerSecond += state.ReceiveBytesPerSecond;
            metrics.SendPacketsPerSecond += state.SendPacketsPerSecond;
            metrics.ReceivePacketsPerSecond += state.ReceivePacketsPerSecond;
        }

        metrics.Devices.Sort((left, right) => left.InterfaceIndex.CompareTo(right.InterfaceIndex));
        metrics.SendMegabytesPerSecond = metrics.SendBytesPerSecond / BytesPerMegabyte;
        metrics.ReceiveMegabytesPerSecond = metrics.ReceiveBytesPerSecond / BytesPerMegabyte;
    }

    private static unsafe bool TryReadInterfaceTable(Dictionary<ulong, InterfaceSample> samples)
    {
        nint table = nint.Zero;
        uint result = IpHlpApi.GetIfTable2(&table);

        if (result != IpHlpApi.ERROR_SUCCESS) {
            PInvokeErrorHelpers.TraceOnceOnPInvokeError(
                nameof(IpHlpApi.GetIfTable2),
                $"Failed {nameof(TryReadInterfaceTable)}",
                result);

            return false;
        }

        if (table == nint.Zero) {
            return false;
        }

        try {
            byte* buffer = (byte*)table;
            uint entryCount = BinaryPrimitives.ReadUInt32LittleEndian(
                new ReadOnlySpan<byte>(buffer + NetIoApi.IfTable2NumEntriesOffset, sizeof(uint)));

            for (uint entry = 0; entry < entryCount; entry++) {
                byte* row = buffer + NetIoApi.IfTable2TableOffset + (entry * (uint)NetIoApi.IfRow2Size);

                ulong interfaceLuid = ReadRowUInt64(row, NetIoApi.IfRow2InterfaceLuidOffset);

                samples[interfaceLuid] = new InterfaceSample(
                    ReadRowUInt32(row, NetIoApi.IfRow2InterfaceIndexOffset),
                    ReadRowUInt64(row, NetIoApi.IfRow2OutOctetsOffset),
                    ReadRowUInt64(row, NetIoApi.IfRow2InOctetsOffset),
                    ReadRowUInt64(row, NetIoApi.IfRow2OutUcastPktsOffset),
                    ReadRowUInt64(row, NetIoApi.IfRow2InUcastPktsOffset));
            }

            return true;
        }
        finally {
            IpHlpApi.FreeMibTable(table);
        }
    }

    // PhysicalMediumType is a spec, but GetAdaptersAddresses does not carry it: it lives only on
    // MIB_IF_ROW2. It is worth the second call because IfType alone is misleading for some
    // adapters, a Bluetooth personal area network reporting IfType 6 (Ethernet) with medium 10
    // (Bluetooth). Runs on the slow specs cadence, not every cycle.
    private static unsafe bool TryReadPhysicalMediums(Dictionary<ulong, uint> mediums)
    {
        nint table = nint.Zero;
        uint result = IpHlpApi.GetIfTable2(&table);

        if (result != IpHlpApi.ERROR_SUCCESS || table == nint.Zero) {
            PInvokeErrorHelpers.TraceOnceOnPInvokeError(
                $"{nameof(IpHlpApi.GetIfTable2)} mediums",
                $"Failed {nameof(TryReadPhysicalMediums)}",
                result);

            return false;
        }

        try {
            byte* buffer = (byte*)table;
            uint entryCount = BinaryPrimitives.ReadUInt32LittleEndian(
                new ReadOnlySpan<byte>(buffer + NetIoApi.IfTable2NumEntriesOffset, sizeof(uint)));

            for (uint entry = 0; entry < entryCount; entry++) {
                byte* row = buffer + NetIoApi.IfTable2TableOffset + (entry * (uint)NetIoApi.IfRow2Size);

                mediums[ReadRowUInt64(row, NetIoApi.IfRow2InterfaceLuidOffset)] =
                    ReadRowUInt32(row, NetIoApi.IfRow2PhysicalMediumTypeOffset);
            }

            return true;
        }
        finally {
            IpHlpApi.FreeMibTable(table);
        }
    }

    private static unsafe uint ReadRowUInt32(byte* row, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(new ReadOnlySpan<byte>(row + offset, sizeof(uint)));

    private static unsafe ulong ReadRowUInt64(byte* row, int offset) =>
        BinaryPrimitives.ReadUInt64LittleEndian(new ReadOnlySpan<byte>(row + offset, sizeof(ulong)));
#endif
}
