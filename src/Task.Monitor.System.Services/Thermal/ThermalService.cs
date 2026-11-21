using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Services.Common;

namespace Task.Monitor.System.Services.Thermal;

public sealed partial class ThermalService : WorkerService
{
    private const int ReprobeEveryCycles = 20;

    private readonly ProviderSet<ThermalSensor> providers = new(nameof(ThermalService), "sensors");

    private int cyclesSinceProbe;

    private partial IEnumerable<IReadingProvider<ThermalSensor>> CreateProviders();

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        if (RescanDue(ref cyclesSinceProbe, ReprobeEveryCycles)) {
            providers.Build(CreateProviders());
        }

        ThermalMetrics metrics = new() { Sensors = providers.Read() };

        try {
            ReadPlatform(metrics);
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex);
        }

        Publish(new ThermalInfo { Metrics = metrics });
    }

    protected override void OnStart() => providers.Build(CreateProviders());

    protected override void OnStop()
    {
        providers.Dispose();
        OnStopPlatform();
    }

    partial void OnStopPlatform();

    partial void ReadPlatform(ThermalMetrics metrics);
}
