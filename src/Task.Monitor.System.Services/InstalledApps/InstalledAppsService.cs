namespace Task.Monitor.System.Services.InstalledApps;

// Publishes the list of applications registered under the Windows Uninstall registry keys - the
// same source Programs and Features / Settings > Apps reads.
//
// Installed apps change even less often than startup configuration, so the scan runs once at start
// and then only on an explicit refresh or every RescanEveryCycles ticks.
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

    // Platform-specific. The Windows implementation lives in
    // InstalledAppsService.InstalledAppsScan.Windows.cs; other platforms get the stub below until
    // they grow one.
    private partial InstalledAppsSpecs ScanInstalledApps();

#if !__WIN32__
    private partial InstalledAppsSpecs ScanInstalledApps() => new();
#endif
}
