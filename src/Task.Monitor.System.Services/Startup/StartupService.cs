using System.Diagnostics;

namespace Task.Monitor.System.Services.Startup;

public sealed partial class StartupService : WorkerService
{
    private const int RescanEveryCycles = 20;

    private StartupSpecs startupSpecs = new();

    private int cyclesSinceScan;
    private int writeCount;

    private readonly List<string> scanNotes = new();

    private partial StartupToggleResult ApplyEnabled(StartupEntry entry, bool enabled);

    private void LogScan(StartupSpecs specs)
    {
        string bySource = string.Join(", ", specs.Entries
            .GroupBy(entry => entry.Source)
            .Select(group => $"{group.Count()} {group.Key}"));

        Trace.WriteLine($"{ServiceName}: {specs.Entries.Count} entries{(bySource.Length > 0 ? $": {bySource}" : string.Empty)}");

        foreach (string note in scanNotes) {
            Trace.WriteLine($"{ServiceName}: {note}");
        }
    }

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        if (ConsumeRefreshRequest() || ++cyclesSinceScan >= RescanEveryCycles) {
            int          writesBefore = Volatile.Read(ref writeCount);
            StartupSpecs specs        = Scan();

            if (Volatile.Read(ref writeCount) == writesBefore) {
                startupSpecs    = specs;
                cyclesSinceScan = 0;
            }
        }

        Publish(new StartupInfo { Specs = startupSpecs });
    }

    protected override void OnStart()
    {
        startupSpecs = Scan();
        LogScan(startupSpecs);
    }

    private StartupSpecs Scan()
    {
        scanNotes.Clear();

        StartupSpecs specs = ScanStartup();

        specs.Entries.Sort(static (left, right) => {
            int bySource = left.Source.CompareTo(right.Source);

            return bySource != 0
                ? bySource
                : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        });

        return specs;
    }

    private partial StartupSpecs ScanStartup();

    public StartupToggleResult SetEnabled(StartupEntry entry, bool enabled)
    {
        StartupEntryState state = enabled ? StartupEntryState.Enabled : StartupEntryState.Disabled;

        if (entry.State == state) {
            return StartupToggleResult.Succeeded;
        }

        StartupToggleResult result = ApplyEnabled(entry, enabled);

        if (result != StartupToggleResult.Succeeded) {
            return result;
        }

        entry.State         = state;
        entry.DisabledOnUtc = enabled ? null : DateTime.UtcNow;

        Interlocked.Increment(ref writeCount);
        RequestImmediateRefresh();

        return result;
    }

    public static bool SupportsSetEnabled { get; } =
#if __WIN32__ || __APPLE__
        true;
#else
        false;
#endif

#if !__WIN32__ && !__APPLE__
    private partial StartupToggleResult ApplyEnabled(StartupEntry entry, bool enabled) =>
        StartupToggleResult.NotSupported;

    private partial StartupSpecs ScanStartup() => new();
#endif
}
