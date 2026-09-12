namespace Task.Monitor.System.Services.WindowsServices;

// Publishes the full Windows service inventory - the same one the Services snap-in (services.msc)
// shows: name, description, status, startup type, and the account each runs as.
//
// Service configuration barely changes, so the scan runs once at start and then only on an
// explicit refresh or every RescanEveryCycles ticks, the same cadence StartupService and
// InstalledAppsService use.
public sealed partial class WindowsServicesService : WorkerService
{
    private const int RescanEveryCycles = 40;

    private WindowsServicesSpecs servicesSpecs = new();
    private int cyclesSinceScan;

    protected override void OnStart() =>
        servicesSpecs = ScanServices();

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        if (ConsumeRefreshRequest() || ++cyclesSinceScan >= RescanEveryCycles) {
            servicesSpecs = ScanServices();
            cyclesSinceScan = 0;
        }

        Publish(new WindowsServicesInfo { Specs = servicesSpecs });
    }

    // Platform-specific. The Windows implementation lives in
    // WindowsServicesService.Windows.cs; other platforms get the stub below until they grow one.
    private partial WindowsServicesSpecs ScanServices();

#if !__WIN32__
    private partial WindowsServicesSpecs ScanServices() => new();
#endif
}
