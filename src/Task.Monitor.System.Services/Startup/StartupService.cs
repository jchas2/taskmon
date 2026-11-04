namespace Task.Monitor.System.Services.Startup;

public sealed partial class StartupService : WorkerService
{
    private const int RescanEveryCycles = 20;

    private StartupSpecs startupSpecs = new();

    private int cyclesSinceScan;
    private int writeCount;

    private partial StartupToggleResult ApplyEnabled(StartupEntry entry, bool enabled);

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

    protected override void OnStart() =>
        startupSpecs = Scan();

    private StartupSpecs Scan()
    {
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
#if __WIN32__
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
