using System.Diagnostics;
using Task.Monitor.System.Services.Common;

namespace Task.Monitor.System.Services.Network;

public sealed partial class NetworkService : WorkerService
{
    private const ulong BitsPerMegabit     = 1_000_000;
    private const int   SpecsRefreshCycles = 10;

    private readonly Dictionary<ulong, InterfaceState> states = new();

    private NetworkSpecs networkSpecs = new();

    private string loggedAdapters = string.Empty;
    private long   previousTimestamp;
    private int    cyclesSinceSpecsRefresh;
    private bool   sawUnknownAdapter;

    private readonly record struct InterfaceSample(
        ulong BytesSent,
        ulong BytesReceived,
        ulong PacketsSent,
        ulong PacketsReceived,
        ulong Errors,
        ulong Discards);

    private sealed class InterfaceState
    {
        public InterfaceSample Last;

        public ulong  TotalBytesSent;
        public ulong  TotalBytesReceived;
        public ulong  TotalPacketsSent;
        public ulong  TotalPacketsReceived;
        public double SendBytesPerSecond;
        public double ReceiveBytesPerSecond;
        public double SendPacketsPerSecond;
        public double ReceivePacketsPerSecond;
        public double ErrorsPerSecond;
        public double DiscardsPerSecond;
    }

    private void BuildMetrics(NetworkMetrics metrics, NetworkSpecs specs)
    {
        foreach (NetworkDevice device in specs.Devices) {
            if (!device.IsActive || !states.TryGetValue(device.InterfaceLuid, out InterfaceState? state)) {
                continue;
            }

            metrics.Devices.Add(new NetworkDeviceMetrics {
                InterfaceIndex          = device.InterfaceIndex,
                InterfaceLuid           = device.InterfaceLuid,
                FriendlyName            = device.FriendlyName,
                TotalBytesSent          = state.TotalBytesSent,
                TotalBytesReceived      = state.TotalBytesReceived,
                TotalPacketsSent        = state.TotalPacketsSent,
                TotalPacketsReceived    = state.TotalPacketsReceived,
                SendBytesPerSecond      = state.SendBytesPerSecond,
                ReceiveBytesPerSecond   = state.ReceiveBytesPerSecond,
                SendPacketsPerSecond    = state.SendPacketsPerSecond,
                ReceivePacketsPerSecond = state.ReceivePacketsPerSecond,
                ErrorsPerSecond         = state.ErrorsPerSecond,
                DiscardsPerSecond       = state.DiscardsPerSecond
            });

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
            metrics.ErrorsPerSecond         += state.ErrorsPerSecond;
            metrics.DiscardsPerSecond       += state.DiscardsPerSecond;
        }
    }

    internal void LogAdapters(NetworkSpecs? specs, bool atStartup)
    {
        if (specs == null) {
            if (atStartup) {
                Trace.WriteLine($"{ServiceName}: adapters FAILED, see the error above");
            }

            return;
        }

        string adapters = string.Join('|', specs.Devices.Select(device => $"{device.FriendlyName} {device.OperationalStatus}"));

        if (adapters == loggedAdapters) {
            return;
        }

        loggedAdapters = adapters;

        Trace.WriteLine(atStartup
            ? $"{ServiceName}: {specs.Devices.Count} adapters"
            : $"{ServiceName}: adapters changed, now {specs.Devices.Count}");

        foreach (NetworkDevice device in specs.Devices) {
            Trace.WriteLine(
                $"{ServiceName}: adapter {device.FriendlyName}, {device.Description}, {device.ConnectionType}, " +
                $"{device.PhysicalMedium}, {device.OperationalStatus}, {device.TransmitLinkSpeed / BitsPerMegabit} Mb/s");
        }
    }

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        SampleInterfaces();

        if (RescanDue(ref cyclesSinceSpecsRefresh, SpecsRefreshCycles) || sawUnknownAdapter) {
            NetworkSpecs? specs = OnStartNetworkSpecs();

            sawUnknownAdapter       = false;
            cyclesSinceSpecsRefresh = 0;

            LogAdapters(specs, atStartup: false);

            if (specs != null) {
                networkSpecs = specs;
            }
        }

        NetworkInfo networkInfo = new() { Specs = networkSpecs };

        BuildMetrics(networkInfo.Metrics, networkSpecs);
        Publish(networkInfo);
    }

    protected override void OnStart()
    {
        NetworkSpecs? specs = OnStartNetworkSpecs();

        LogAdapters(specs, atStartup: true);

        if (specs != null) {
            networkSpecs = specs;
        }
    }

    protected override void OnStop()
    {
        states.Clear();

        previousTimestamp       = 0;
        cyclesSinceSpecsRefresh = 0;
        sawUnknownAdapter       = false;
    }

    private void SampleInterfaces()
    {
        Dictionary<ulong, InterfaceSample> samples = new();

        bool read = TryReadInterfaces(samples);

        if (IsFirstCycle) {
            Trace.WriteLine(read
                ? $"{ServiceName}: {InterfaceCounters} OK, {samples.Count} interfaces"
                : $"{ServiceName}: {InterfaceCounters} FAILED, {InterfaceCountersFailure}");
        }

        if (!read) {
            return;
        }

        long timestamp = Stopwatch.GetTimestamp();

        if (previousTimestamp == 0) {
            foreach ((ulong key, InterfaceSample sample) in samples) {
                states[key] = new InterfaceState { Last = sample };
            }

            previousTimestamp = timestamp;
            return;
        }

        double elapsedSeconds = Stopwatch.GetElapsedTime(previousTimestamp, timestamp).TotalSeconds;

        if (elapsedSeconds <= 0.0) {
            return;
        }

        foreach ((ulong key, InterfaceSample sample) in samples) {
            UpdateState(key, sample, elapsedSeconds);
        }

        previousTimestamp = timestamp;
    }

    private void UpdateState(ulong key, InterfaceSample sample, double elapsedSeconds)
    {
        if (!states.TryGetValue(key, out InterfaceState? state)) {
            states[key]       = new InterfaceState { Last = sample };
            sawUnknownAdapter = true;
            return;
        }

        InterfaceSample last = state.Last;

        double Rate(ulong current, ulong previous, ref ulong total)
        {
            ulong delta = CounterRate.Delta(current, previous);

            total += delta;
            return delta / elapsedSeconds;
        }

        state.SendBytesPerSecond      = Rate(sample.BytesSent,       last.BytesSent,       ref state.TotalBytesSent);
        state.ReceiveBytesPerSecond   = Rate(sample.BytesReceived,   last.BytesReceived,   ref state.TotalBytesReceived);
        state.SendPacketsPerSecond    = Rate(sample.PacketsSent,     last.PacketsSent,     ref state.TotalPacketsSent);
        state.ReceivePacketsPerSecond = Rate(sample.PacketsReceived, last.PacketsReceived, ref state.TotalPacketsReceived);
        state.ErrorsPerSecond         = CounterRate.Delta(sample.Errors, last.Errors) / elapsedSeconds;
        state.DiscardsPerSecond       = CounterRate.Delta(sample.Discards, last.Discards) / elapsedSeconds;
        state.Last                    = sample;
    }
}
