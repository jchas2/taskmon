using Task.Monitor.Cli.Utils;

namespace Task.Monitor.System.Services.Power;

// Publishes device power draw: NVIDIA and AMD GPUs (measured), the whole system on battery, an
// ACPI power meter where present, and an NVMe drive's rated peak power. There is no CPU package
// power without a kernel driver, so on most desktops this reports GPU only.
public sealed partial class PowerService : WorkerService
{
    private const int ReprobeEveryCycles = 20;

    private readonly List<IPowerProvider> providers = new();
    private int cyclesSinceProbe;

    protected override void OnStart() => BuildProviders();

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        if (ConsumeRefreshRequest() || ++cyclesSinceProbe >= ReprobeEveryCycles) {
            BuildProviders();
        }

        List<PowerReading> readings = new();

        foreach (IPowerProvider provider in providers) {
            try {
                readings.AddRange(provider.Read());
            }
            catch (Exception ex) {
                ExceptionHelper.LogException(ex);
            }
        }

        Publish(new PowerInfo { Metrics = new PowerMetrics { Readings = readings } });
    }

    protected override void OnStop() => DisposeProviders();

    private void BuildProviders()
    {
        DisposeProviders();
        cyclesSinceProbe = 0;

        foreach (IPowerProvider provider in CreateProviders()) {
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
        foreach (IPowerProvider provider in providers) {
            TryDispose(provider);
        }

        providers.Clear();
    }

    private static void TryDispose(IPowerProvider provider)
    {
        try {
            provider.Dispose();
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex);
        }
    }

    private partial IEnumerable<IPowerProvider> CreateProviders();

#if !__WIN32__
    private partial IEnumerable<IPowerProvider> CreateProviders() => [];
#endif
}
