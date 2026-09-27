namespace Task.Monitor.System.Services.WindowsServices;

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

    private partial WindowsServicesSpecs ScanServices();

#if !__WIN32__
    private partial WindowsServicesSpecs ScanServices() => new();
#endif
}
