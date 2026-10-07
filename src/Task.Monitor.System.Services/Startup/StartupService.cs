namespace Task.Monitor.System.Services.Startup;

public sealed partial class StartupService : WorkerService
{
    private const int RescanEveryCycles = 20;

    private StartupSpecs startupSpecs = new();
    private int cyclesSinceScan;

    protected override void OnStart() =>
        startupSpecs = Scan();

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        if (ConsumeRefreshRequest() || ++cyclesSinceScan >= RescanEveryCycles) {
            startupSpecs = Scan();
            cyclesSinceScan = 0;
        }

        Publish(new StartupInfo { Specs = startupSpecs });
    }

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

#if !__WIN32__ && !__APPLE__
    private partial StartupSpecs ScanStartup() => new();
#endif
}
