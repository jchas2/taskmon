using Task.Monitor.Cli.Utils;

namespace Task.Monitor.System.Services.Thermal;

// Publishes every temperature the machine will report without a kernel driver: NVMe / ATA drive
// sensors, NVIDIA and AMD GPU sensors, and the ACPI thermal zones (the only, approximate, CPU
// source). Providers are probed once at start; a provider that fails to initialise or throws is
// dropped and re-probed on the next refresh.
public sealed partial class ThermalService : WorkerService
{
    private const int ReprobeEveryCycles = 20;

    private readonly List<IThermalProvider> providers = new();
    private int cyclesSinceProbe;

    protected override void OnStart() => BuildProviders();

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

        Publish(new ThermalInfo { Metrics = new ThermalMetrics { Sensors = sensors } });
    }

    protected override void OnStop() => DisposeProviders();

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

    private void DisposeProviders()
    {
        foreach (IThermalProvider provider in providers) {
            TryDispose(provider);
        }

        providers.Clear();
    }

    private static void TryDispose(IThermalProvider provider)
    {
        try {
            provider.Dispose();
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex);
        }
    }

    // Platform-specific. The Windows list is in ThermalService.Providers.Windows.cs; other
    // platforms get the empty stub below until they grow one.
    private partial IEnumerable<IThermalProvider> CreateProviders();

#if !__WIN32__
    private partial IEnumerable<IThermalProvider> CreateProviders() => [];
#endif
}
