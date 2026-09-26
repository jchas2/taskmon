namespace Task.Monitor.System.Services.InstalledApps;

public sealed partial class InstalledAppsService : WorkerService
{
    private const int RescanEveryCycles = 40;

    private InstalledAppsSpecs installedAppsSpecs = new();
    private int cyclesSinceScan;

    protected override void OnStart() =>
        installedAppsSpecs = ScanInstalledApps();

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        if (ConsumeRefreshRequest() || ++cyclesSinceScan >= RescanEveryCycles) {
            installedAppsSpecs = ScanInstalledApps();
            cyclesSinceScan = 0;
        }

        Publish(new InstalledAppsInfo { Specs = installedAppsSpecs });
    }

    private partial InstalledAppsSpecs ScanInstalledApps();

#if !__WIN32__
    private partial InstalledAppsSpecs ScanInstalledApps() => new();
#endif
}
