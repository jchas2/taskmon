using System.Diagnostics;
using Task.Monitor.Cli.Utils;

namespace Task.Monitor.System.Services.Common;

internal sealed class ProviderSet<T>(string owner, string unit) : IDisposable
{
    private readonly List<IReadingProvider<T>> providers = [];

    private List<ProviderResult>? pendingResults;
    private string?               loggedResults;

    internal readonly record struct ProviderResult(string Name, string Result, IReadingProvider<T>? Provider);

    public void Build(IEnumerable<IReadingProvider<T>> created)
    {
        Dispose();

        List<ProviderResult> results = [];

        foreach (IReadingProvider<T> provider in created) {
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

    public void Dispose()
    {
        foreach (IReadingProvider<T> provider in providers) {
            TryDispose(provider);
        }

        providers.Clear();
    }

    internal void Log(List<ProviderResult> results, Dictionary<IReadingProvider<T>, int> counts)
    {
        string resultsText = string.Join('|', results.Select(result => $"{result.Name} {result.Result}"));

        if (resultsText == loggedResults) {
            return;
        }

        if (loggedResults != null) {
            Trace.WriteLine($"{owner}: providers changed");
        }

        loggedResults = resultsText;

        foreach (ProviderResult result in results) {
            Trace.WriteLine(result.Provider is null
                ? $"{owner}: {result.Name} {result.Result}"
                : $"{owner}: {result.Name} {result.Result}, {counts.GetValueOrDefault(result.Provider)} {unit}");
        }
    }

    public List<T> Read()
    {
        List<T> readings = [];
        
        Dictionary<IReadingProvider<T>, int> counts   = new();

        foreach (IReadingProvider<T> provider in providers) {
            try {
                int before = readings.Count;
                readings.AddRange(provider.Read());
                counts[provider] = readings.Count - before;
            }
            catch (Exception ex) {
                ExceptionHelper.LogException(ex);
            }
        }

        if (pendingResults != null) {
            Log(pendingResults, counts);
            pendingResults = null;
        }

        return readings;
    }

    private static string ResultOf(IReadingProvider<T> provider, string otherwise) =>
        provider.Diagnostic.Length > 0 ? provider.Diagnostic : otherwise;

    private static void TryDispose(IReadingProvider<T> provider)
    {
        try {
            provider.Dispose();
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex);
        }
    }
}
