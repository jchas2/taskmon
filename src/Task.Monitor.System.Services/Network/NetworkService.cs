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

        OnDoWorkNetworkSample();
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

    protected override void OnStop() => OnStopNetworkMetrics();
}
