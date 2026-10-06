using System.Diagnostics;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Win32;

namespace Task.Monitor.System.Services.Network;

public partial class NetworkService
{
#if __WIN32__
    private const double BytesPerMegabyte = 1024.0 * 1024.0;
    private const int SpecsRefreshCycles = 10;

    private readonly Dictionary<ulong, NetworkInstanceState> instanceStates = new();
    private long previousTimestamp;
    private bool primed;
    private int cyclesSinceSpecsRefresh;
    private bool sawUnknownAdapter;

    private sealed class NetworkInstanceState
    {
        public uint   InterfaceIndex;
        public ulong  PreviousBytesSent;
        public ulong  PreviousBytesReceived;
        public ulong  PreviousPacketsSent;
        public ulong  PreviousPacketsReceived;
        public bool   Primed;

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
            PrimeInstanceState(interfaceLuid, sample);
            sawUnknownAdapter = true;
            return;
        }

        state.InterfaceIndex = sample.InterfaceIndex;

        state.SendBytesPerSecond = AccumulateDelta(
            sample.BytesSent, 
            ref state.PreviousBytesSent, 
            ref state.TotalBytesSent, 
            elapsedSeconds);

        state.ReceiveBytesPerSecond = AccumulateDelta(
            sample.BytesReceived, 
            ref state.PreviousBytesReceived, 
            ref state.TotalBytesReceived, 
            elapsedSeconds);

        state.SendPacketsPerSecond = AccumulateDelta(
            sample.PacketsSent, 
            ref state.PreviousPacketsSent, 
            ref state.TotalPacketsSent, 
            elapsedSeconds);

        state.ReceivePacketsPerSecond = AccumulateDelta(
            sample.PacketsReceived, 
            ref state.PreviousPacketsReceived, 
            ref state.TotalPacketsReceived, 
            elapsedSeconds);
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
            deviceMetrics.InterfaceIndex            = device.InterfaceIndex;
            deviceMetrics.InterfaceLuid             = luid;
            deviceMetrics.FriendlyName              = device.FriendlyName;
            deviceMetrics.TotalBytesSent            = state.TotalBytesSent;
            deviceMetrics.TotalBytesReceived        = state.TotalBytesReceived;
            deviceMetrics.TotalPacketsSent          = state.TotalPacketsSent;
            deviceMetrics.TotalPacketsReceived      = state.TotalPacketsReceived;
            deviceMetrics.SendBytesPerSecond        = state.SendBytesPerSecond;
            deviceMetrics.ReceiveBytesPerSecond     = state.ReceiveBytesPerSecond;
            deviceMetrics.SendPacketsPerSecond      = state.SendPacketsPerSecond;
            deviceMetrics.ReceivePacketsPerSecond   = state.ReceivePacketsPerSecond;
            deviceMetrics.SendMegabytesPerSecond    = state.SendBytesPerSecond / BytesPerMegabyte;
            deviceMetrics.ReceiveMegabytesPerSecond = state.ReceiveBytesPerSecond / BytesPerMegabyte;

            metrics.Devices.Add(deviceMetrics);

            if (!device.CountsTowardAggregate) {
                continue;
            }

            metrics.TotalBytesSent          += state.TotalBytesSent;
            metrics.TotalBytesReceived      += state.TotalBytesReceived;
            metrics.TotalPacketsSent        += state.TotalPacketsSent;
            metrics.TotalPacketsReceived    += state.TotalPacketsReceived;
            metrics.SendBytesPerSecond      += state.SendBytesPerSecond;
            metrics.ReceiveBytesPerSecond   += state.ReceiveBytesPerSecond;
            metrics.SendPacketsPerSecond    += state.SendPacketsPerSecond;
            metrics.ReceivePacketsPerSecond += state.ReceivePacketsPerSecond;
        }

        metrics.Devices.Sort((left, right) => left.InterfaceIndex.CompareTo(right.InterfaceIndex));
        metrics.SendMegabytesPerSecond    = metrics.SendBytesPerSecond / BytesPerMegabyte;
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
            uint entryCount = InteropHelper.ReadUInt32(buffer, NetIoApi.IfTable2NumEntriesOffset);

            for (uint entry = 0; entry < entryCount; entry++) {
                byte* row = buffer + NetIoApi.IfTable2TableOffset + (entry * (uint)NetIoApi.IfRow2Size);

                ulong interfaceLuid = InteropHelper.ReadUInt64(row, NetIoApi.IfRow2InterfaceLuidOffset);

                samples[interfaceLuid] = new InterfaceSample(
                    InteropHelper.ReadUInt32(row, NetIoApi.IfRow2InterfaceIndexOffset),
                    InteropHelper.ReadUInt64(row, NetIoApi.IfRow2OutOctetsOffset),
                    InteropHelper.ReadUInt64(row, NetIoApi.IfRow2InOctetsOffset),
                    InteropHelper.ReadUInt64(row, NetIoApi.IfRow2OutUcastPktsOffset),
                    InteropHelper.ReadUInt64(row, NetIoApi.IfRow2InUcastPktsOffset));
            }
            
            return true;
        }
        finally {
            IpHlpApi.FreeMibTable(table);
        }
    }

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
            uint entryCount = InteropHelper.ReadUInt32(buffer, NetIoApi.IfTable2NumEntriesOffset);

            for (uint entry = 0; entry < entryCount; entry++) {
                byte* row = buffer + NetIoApi.IfTable2TableOffset + (entry * (uint)NetIoApi.IfRow2Size);

                mediums[InteropHelper.ReadUInt64(row, NetIoApi.IfRow2InterfaceLuidOffset)] =
                    InteropHelper.ReadUInt32(row, NetIoApi.IfRow2PhysicalMediumTypeOffset);
            }

            return true;
        }
        finally {
            IpHlpApi.FreeMibTable(table);
        }
    }
#endif
}
