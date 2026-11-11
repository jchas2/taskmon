using System.Diagnostics;
using Task.Monitor.Cli.Utils;

namespace Task.Monitor.System.Services.Thermal;

public sealed partial class ThermalService : WorkerService
{
    private const int ReprobeEveryCycles = 20;

    private readonly List<IThermalProvider> providers = new();

    private int cyclesSinceProbe;

    // Each provider's result from the last build, waiting for the next read so a working provider's
    // line can give its sensor count. The logged text is compared so a re-probe that finds the same
    // providers writes nothing.
    private List<ProviderResult>? pendingResults;
    private string?               loggedResults;

    internal readonly record struct ProviderResult(string Name, string Result, IThermalProvider? Provider);

    private void BuildProviders()
    {
        DisposeProviders();
        cyclesSinceProbe = 0;

        List<ProviderResult> results = new();

        foreach (IThermalProvider provider in CreateProviders()) {
            try {
                if (provider.TryInitialise()) {
                    providers.Add(provider);
                    results.Add(new ProviderResult(provider.Name, ResultOf(provider, "OK"), provider));
                }
                else {
                    results.Add(new ProviderResult(provider.Name, ResultOf(provider, "NOT FOUND"), null));
                    provider.Dispose();
                }
            }
            catch (Exception ex) {
                results.Add(new ProviderResult(provider.Name, $"FAILED with {ex.GetType().Name}: {ex.Message}", null));
                ExceptionHelper.LogException(ex);
                TryDispose(provider);
            }
        }

        pendingResults = results;
    }

    private partial IEnumerable<IThermalProvider> CreateProviders();

    private void DisposeProviders()
    {
        foreach (IThermalProvider provider in providers) {
            TryDispose(provider);
        }

        providers.Clear();
    }

    internal void LogProviders(List<ProviderResult> results, Dictionary<IThermalProvider, int> sensorCounts)
    {
        string resultsText = string.Join('|', results.Select(result => $"{result.Name} {result.Result}"));

        if (resultsText == loggedResults) {
            return;
        }

        if (loggedResults != null) {
            Trace.WriteLine($"{ServiceName}: providers changed");
        }

        loggedResults = resultsText;

        foreach (ProviderResult result in results) {
            Trace.WriteLine(result.Provider is null
                ? $"{ServiceName}: {result.Name} {result.Result}"
                : $"{ServiceName}: {result.Name} {result.Result}, {sensorCounts.GetValueOrDefault(result.Provider)} sensors");
        }
    }

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        if (ConsumeRefreshRequest() || ++cyclesSinceProbe >= ReprobeEveryCycles) {
            BuildProviders();
        }

        List<ThermalSensor>                sensors      = new();
        Dictionary<IThermalProvider, int> sensorCounts = new();

        foreach (IThermalProvider provider in providers) {
            try {
                int before = sensors.Count;
                sensors.AddRange(provider.Read());
                sensorCounts[provider] = sensors.Count - before;
            }
            catch (Exception ex) {
                ExceptionHelper.LogException(ex);
            }
        }

        if (pendingResults != null) {
            LogProviders(pendingResults, sensorCounts);
            pendingResults = null;
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

    private static string ResultOf(IThermalProvider provider, string otherwise) =>
        provider.Diagnostic.Length > 0 ? provider.Diagnostic : otherwise;

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
