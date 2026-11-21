using Task.Monitor.System.Services.Common;

namespace Task.Monitor.System.Services.Power;

public sealed partial class PowerService : WorkerService
{
    private const int ReprobeEveryCycles = 20;

    private readonly ProviderSet<PowerReading> providers = new(nameof(PowerService), "readings");

    private int cyclesSinceProbe;

    private partial IEnumerable<IReadingProvider<PowerReading>> CreateProviders();

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        if (RescanDue(ref cyclesSinceProbe, ReprobeEveryCycles)) {
            providers.Build(CreateProviders());
        }

        Publish(new PowerInfo { Metrics = new PowerMetrics { Readings = providers.Read() } });
    }

    protected override void OnStart() => providers.Build(CreateProviders());

    protected override void OnStop()
    {
        providers.Dispose();
        OnStopPlatform();
    }

    partial void OnStopPlatform();
}
