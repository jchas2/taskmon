#if __APPLE__
using System.Diagnostics;
using System.Runtime.InteropServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Network;

public partial class NetworkService
{
    private const double BytesPerMegabyte = 1024.0 * 1024.0;
    private const int SpecsRefreshCycles = 10;

    // Keyed by BSD interface index (if_msghdr2.ifm_index), which also matches the index exposed by
    // the BCL NetworkInterface used for specs.
    private readonly Dictionary<uint, NetworkInstanceState> instanceStates = new();
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

        public ulong  TotalBytesSent;
        public ulong  TotalBytesReceived;
        public ulong  TotalPacketsSent;
        public ulong  TotalPacketsReceived;
        public double SendBytesPerSecond;
        public double ReceiveBytesPerSecond;
        public double SendPacketsPerSecond;
        public double ReceivePacketsPerSecond;
    }

    private readonly struct InterfaceSample(
        uint interfaceIndex,
        ulong bytesSent,
        ulong bytesReceived,
        ulong packetsSent,
        ulong packetsReceived)
    {
        public uint  InterfaceIndex  { get; } = interfaceIndex;
        public ulong BytesSent       { get; } = bytesSent;
        public ulong BytesReceived   { get; } = bytesReceived;
        public ulong PacketsSent     { get; } = packetsSent;
        public ulong PacketsReceived { get; } = packetsReceived;
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
        Dictionary<uint, InterfaceSample> samples = new();

        if (!TryReadInterfaceList(samples)) {
            return;
        }

        long timestamp = Stopwatch.GetTimestamp();
        long elapsedTicks = timestamp - previousTimestamp;

        if (!primed) {
            foreach ((uint index, InterfaceSample sample) in samples) {
                PrimeInstanceState(index, sample);
            }

            previousTimestamp = timestamp;
            primed = true;
            return;
        }

        if (elapsedTicks <= 0) {
            return;
        }

        double elapsedSeconds = elapsedTicks / (double)Stopwatch.Frequency;

        foreach ((uint index, InterfaceSample sample) in samples) {
            UpdateInstanceState(index, sample, elapsedSeconds);
        }

        previousTimestamp = timestamp;
    }

    private void PrimeInstanceState(uint interfaceIndex, InterfaceSample sample)
    {
        NetworkInstanceState state = new();
        state.InterfaceIndex = interfaceIndex;
        state.PreviousBytesSent = sample.BytesSent;
        state.PreviousBytesReceived = sample.BytesReceived;
        state.PreviousPacketsSent = sample.PacketsSent;
        state.PreviousPacketsReceived = sample.PacketsReceived;

        instanceStates[interfaceIndex] = state;
    }

    private void UpdateInstanceState(uint interfaceIndex, InterfaceSample sample, double elapsedSeconds)
    {
        if (!instanceStates.TryGetValue(interfaceIndex, out NetworkInstanceState? state)) {
            PrimeInstanceState(interfaceIndex, sample);
            sawUnknownAdapter = true;
            return;
        }

        state.SendBytesPerSecond = AccumulateDelta(
            sample.BytesSent, ref state.PreviousBytesSent, ref state.TotalBytesSent, elapsedSeconds);

        state.ReceiveBytesPerSecond = AccumulateDelta(
            sample.BytesReceived, ref state.PreviousBytesReceived, ref state.TotalBytesReceived, elapsedSeconds);

        state.SendPacketsPerSecond = AccumulateDelta(
            sample.PacketsSent, ref state.PreviousPacketsSent, ref state.TotalPacketsSent, elapsedSeconds);

        state.ReceivePacketsPerSecond = AccumulateDelta(
            sample.PacketsReceived, ref state.PreviousPacketsReceived, ref state.TotalPacketsReceived, elapsedSeconds);
    }

    private static double AccumulateDelta(ulong current, ref ulong previous, ref ulong total, double elapsedSeconds)
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

    // Walks the NET_RT_IFLIST2 route messages (same source as `netstat -ib`) and records the
    // cumulative byte/packet counters for each interface by its BSD index.
    private static unsafe bool TryReadInterfaceList(Dictionary<uint, InterfaceSample> samples)
    {
        ReadOnlySpan<int> name = [
            (int)Sys.Selectors.CTL_NET,
            Sys.PF_ROUTE,
            0,
            Sys.AF_UNSPEC,
            (int)Sys.NetRouting.NET_RT_IFLIST2,
            0
        ];

        byte* buffer = null;
        int length = 0;

        bool read = Sys.Sysctl(name, ref buffer, ref length);
        using HGlobalScope scope = new((nint)buffer);

        if (!read || buffer == null || length == 0) {
            return false;
        }

        byte* current = buffer;
        byte* end = buffer + length;
        int headerSize = Marshal.SizeOf<Sys.if_msghdr2>();

        while (current < end) {
            if (current + sizeof(ushort) > end) {
                break;
            }

            ushort msgLen = *(ushort*)current;

            if (msgLen == 0 || current + msgLen > end) {
                break;
            }

            // Byte 3 is ifm_type; RTM_IFINFO2 rows carry the per-interface statistics.
            if (current + 4 <= end && *(current + 3) == Sys.RTM_IFINFO2 && current + headerSize <= end) {
                Sys.if_msghdr2* ifMsg = (Sys.if_msghdr2*)current;
                uint index = ifMsg->ifm_index;

                samples[index] = new InterfaceSample(
                    index,
                    ifMsg->ifm_data.ifi_obytes,
                    ifMsg->ifm_data.ifi_ibytes,
                    ifMsg->ifm_data.ifi_opackets,
                    ifMsg->ifm_data.ifi_ipackets);
            }

            current += msgLen;
        }

        return samples.Count > 0;
    }
}
#endif
