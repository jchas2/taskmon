namespace Task.Monitor.System.Services.Startup;

// Publishes the list of applications configured to run at logon: the Run / RunOnce registry keys
// and the Startup folders, with the enabled/disabled state Task Manager records.
//
// Startup configuration barely changes, so the scan runs once at start and then only on an
// explicit refresh or every RescanEveryCycles ticks - enough to notice an install or uninstall
// without a dedicated registry watcher.
public sealed partial class StartupService : WorkerService
{
    private const int RescanEveryCycles = 20;

    private StartupSpecs startupSpecs = new();
    private int cyclesSinceScan;

    protected override void OnStart() =>
        startupSpecs = ScanStartup();

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        if (ConsumeRefreshRequest() || ++cyclesSinceScan >= RescanEveryCycles) {
            startupSpecs = ScanStartup();
            cyclesSinceScan = 0;
        }

        Publish(new StartupInfo { Specs = startupSpecs });
    }

    // Platform-specific. The Windows implementation lives in StartupService.StartupScan.Windows.cs;
    // other platforms get the stub below until they grow one.
    private partial StartupSpecs ScanStartup();

#if !__WIN32__
    private partial StartupSpecs ScanStartup() => new();
#endif
}
