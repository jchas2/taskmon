namespace Task.Monitor.System.Services.Network;

public sealed class NetworkInfo
{
    public NetworkSpecs Specs { get; set; } = new();
    public NetworkMetrics Metrics { get; set; } = new();
}
