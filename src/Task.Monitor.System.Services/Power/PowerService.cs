using System.Diagnostics;
using Task.Monitor.Cli.Utils;

namespace Task.Monitor.System.Services.Power;

public sealed partial class PowerService : WorkerService
{
    private const int ReprobeEveryCycles = 20;

    private readonly List<IPowerProvider> providers = new();
    
    private int cyclesSinceProbe;

    private List<ProviderResult>? pendingResults;
    private string?               loggedResults;

    private readonly record struct ProviderResult(string Name, string Result, IPowerProvider? Provider);

    private void BuildProviders()
    {
        DisposeProviders();
        cyclesSinceProbe = 0;

        List<ProviderResult> results = new();

        foreach (IPowerProvider provider in CreateProviders()) {
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

    private partial IEnumerable<IPowerProvider> CreateProviders();

    private void DisposeProviders()
    {
        foreach (IPowerProvider provider in providers) {
            TryDispose(provider);
        }

        providers.Clear();
    }

    private void LogProviders(List<ProviderResult> results, Dictionary<IPowerProvider, int> readingCounts)
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
                : $"{ServiceName}: {result.Name} {result.Result}, {readingCounts.GetValueOrDefault(result.Provider)} readings");
        }
    }

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        if (ConsumeRefreshRequest() || ++cyclesSinceProbe >= ReprobeEveryCycles) {
            BuildProviders();
        }

        List<PowerReading>              readings      = new();
        Dictionary<IPowerProvider, int> readingCounts = new();

        foreach (IPowerProvider provider in providers) {
            try {
                int before = readings.Count;
                readings.AddRange(provider.Read());
                readingCounts[provider] = readings.Count - before;
            }
            catch (Exception ex) {
                ExceptionHelper.LogException(ex);
            }
        }

        if (pendingResults != null) {
            LogProviders(pendingResults, readingCounts);
            pendingResults = null;
        }

        Publish(new PowerInfo { Metrics = new PowerMetrics { Readings = readings } });
    }

    protected override void OnStart() => BuildProviders();

    protected override void OnStop()
    {
        DisposeProviders();
        OnStopPlatform();
    }

    partial void OnStopPlatform();

    private static string ResultOf(IPowerProvider provider, string otherwise) =>
        provider.Diagnostic.Length > 0 ? provider.Diagnostic : otherwise;

    private static void TryDispose(IPowerProvider provider)
    {
        try {
            provider.Dispose();
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex);
        }
    }

#if !__WIN32__ && !__APPLE__
    private partial IEnumerable<IPowerProvider> CreateProviders() => [];
#endif
}
