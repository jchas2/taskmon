namespace Task.Monitor.System.Services.Drivers;

// Publishes the kernel-mode and file-system drivers registered with the Service Control Manager -
// name, status, startup type, install path and file version.
//
// Driver registration barely changes at runtime, so the scan runs once at start and then only on
// an explicit refresh or every RescanEveryCycles ticks, the same cadence WindowsServicesService,
// StartupService and InstalledAppsService use.
public sealed partial class DriversService : WorkerService
{
    private const int RescanEveryCycles = 40;

    private DriversSpecs driversSpecs = new();
    private int cyclesSinceScan;

    protected override void OnStart() =>
        driversSpecs = ScanDrivers();

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        if (ConsumeRefreshRequest() || ++cyclesSinceScan >= RescanEveryCycles) {
            driversSpecs = ScanDrivers();
            cyclesSinceScan = 0;
        }

        Publish(new DriversInfo { Specs = driversSpecs });
    }

    // Platform-specific. The Windows implementation lives in DriversService.Windows.cs; other
    // platforms get the stub below until they grow one.
    private partial DriversSpecs ScanDrivers();

#if !__WIN32__
    private partial DriversSpecs ScanDrivers() => new();
#endif
}
