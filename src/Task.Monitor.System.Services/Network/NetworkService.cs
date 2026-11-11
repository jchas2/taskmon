using System.Diagnostics;

namespace Task.Monitor.System.Services.Network;

public sealed partial class NetworkService : WorkerService
{
    private const ulong BitsPerMegabit = 1_000_000;

    private NetworkSpecs networkSpecs = new();

    private string loggedAdapters = string.Empty;

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
        NetworkInfo networkInfo = new();

        OnDoWorkNetworkSample();
        bool deviceChanged = ConsumeRefreshRequest();

        if (ShouldRefreshNetworkSpecs(networkSpecs) || deviceChanged) {
            NetworkSpecs? specs = OnStartNetworkSpecs();

            LogAdapters(specs, atStartup: false);

            if (specs != null) {
                networkSpecs = specs;
            }
        }

        networkInfo.Specs = networkSpecs;
        OnBuildNetworkMetrics(networkInfo.Metrics, networkSpecs);
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

    protected override void OnStop() => OnStopNetworkMetrics();
}
