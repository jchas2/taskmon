#if __APPLE__
namespace Task.Monitor.System.Services.Network;

// macOS stub: the service runs and publishes empty specs and metrics until a macOS implementation
// replaces these.
public sealed partial class NetworkService
{
    // Null leaves the service's default (empty) specs in place.
    private NetworkSpecs? OnStartNetworkSpecs() => null;

    private void OnDoWorkNetworkSample() { }

    private bool ShouldRefreshNetworkSpecs(NetworkSpecs specs) => false;

    private void OnBuildNetworkMetrics(NetworkMetrics metrics, NetworkSpecs specs) { }

    private void OnStopNetworkMetrics() { }
}
#endif
