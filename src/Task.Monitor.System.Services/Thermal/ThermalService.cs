using Task.Monitor.Cli.Utils;

namespace Task.Monitor.System.Services.Thermal;

public sealed partial class ThermalService : WorkerService
{
    private const int ReprobeEveryCycles = 20;

    private readonly List<IThermalProvider> providers = new();
    
    private int cyclesSinceProbe;

    private void BuildProviders()
    {
        DisposeProviders();
        cyclesSinceProbe = 0;

        foreach (IThermalProvider provider in CreateProviders()) {
            try {
                if (provider.TryInitialise()) {
                    providers.Add(provider);
                }
                else {
                    provider.Dispose();
                }
            }
            catch (Exception ex) {
                ExceptionHelper.LogException(ex);
                TryDispose(provider);
            }
        }
    }

    private partial IEnumerable<IThermalProvider> CreateProviders();

    private void DisposeProviders()
    {
        foreach (IThermalProvider provider in providers) {
            TryDispose(provider);
        }

        providers.Clear();
    }

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        if (ConsumeRefreshRequest() || ++cyclesSinceProbe >= ReprobeEveryCycles) {
            BuildProviders();
        }

        List<ThermalSensor> sensors = new();

        foreach (IThermalProvider provider in providers) {
            try {
                sensors.AddRange(provider.Read());
            }
            catch (Exception ex) {
                ExceptionHelper.LogException(ex);
            }
        }

        ThermalMetrics metrics = new() { Sensors = sensors };

        try {
            ReadPlatform(metrics);
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex);
        }

        Publish(new ThermalInfo { Metrics = metrics });
    }

    protected override void OnStart() => BuildProviders();

    protected override void OnStop()
    {
        DisposeProviders();
        OnStopPlatform();
    }

    partial void OnStopPlatform();

    partial void ReadPlatform(ThermalMetrics metrics);

    private static void TryDispose(IThermalProvider provider)
    {
        try {
            provider.Dispose();
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex);
        }
    }

#if !__WIN32__ && !__APPLE__
    private partial IEnumerable<IThermalProvider> CreateProviders() => [];
#endif
}
