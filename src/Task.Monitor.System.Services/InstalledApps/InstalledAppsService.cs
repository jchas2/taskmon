namespace Task.Monitor.System.Services.InstalledApps;

public sealed partial class InstalledAppsService : WorkerService
{
    private const int RescanEveryCycles = 40;

    // How long one cycle may spend measuring app sizes before publishing what it has so far.
    private static readonly TimeSpan SizeBudget = TimeSpan.FromMilliseconds(750);

    private readonly InstalledAppSizeCache sizeCache = new();

    private InstalledAppsSpecs installedAppsSpecs = new();
    
    private int cyclesSinceScan;

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        if (ConsumeRefreshRequest() || ++cyclesSinceScan >= RescanEveryCycles) {
            installedAppsSpecs = ScanInstalledApps();
            cyclesSinceScan = 0;
        }
        else {
            // Only macOS scans leave sizes pending; elsewhere this returns the specs unchanged.
            installedAppsSpecs = sizeCache.ComputePending(installedAppsSpecs, SizeBudget, cancellationToken);
        }

        Publish(new InstalledAppsInfo { Specs = installedAppsSpecs });
    }

    protected override void OnStart() =>
        installedAppsSpecs = ScanInstalledApps();

    private partial InstalledAppsSpecs ScanInstalledApps();

#if !__WIN32__ && !__APPLE__
    private partial InstalledAppsSpecs ScanInstalledApps() => new();
#endif
}
