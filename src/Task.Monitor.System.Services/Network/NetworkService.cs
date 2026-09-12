namespace Task.Monitor.System.Services.Network;

public sealed partial class NetworkService : WorkerService
{
    private NetworkSpecs networkSpecs = new();

    protected override void OnStart()
    {
        NetworkSpecs? specs = OnStartNetworkSpecs();

        if (specs != null) {
            networkSpecs = specs;
        }
    }

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        NetworkInfo networkInfo = new();

        // Sample first, then reconcile the adapter list. Unlike disks, adapters come and go while
        // the service runs (a cable is plugged in, Wi-Fi associates, a VPN comes up), so the
        // specs cannot be captured once at start the way the disk service captures its drives.
        // The sample is what tells us an unknown adapter has appeared.
        OnDoWorkNetworkSample();

        // ConsumeRefreshRequest first so an adapter removal notification forces the reconcile
        // this cycle instead of waiting out SpecsRefreshCycles; ShouldRefreshNetworkSpecs still
        // advances its own counter regardless.
        bool deviceChanged = ConsumeRefreshRequest();

        if (ShouldRefreshNetworkSpecs(networkSpecs) || deviceChanged) {
            NetworkSpecs? specs = OnStartNetworkSpecs();

            if (specs != null) {
                networkSpecs = specs;
            }
        }

        networkInfo.Specs = networkSpecs;
        OnBuildNetworkMetrics(networkInfo.Metrics, networkSpecs);
        Publish(networkInfo);
    }

    protected override void OnStop()
    {
        OnStopNetworkMetrics();
    }
}
