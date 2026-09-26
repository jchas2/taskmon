namespace Task.Monitor.System.Services.Drivers;

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

    private partial DriversSpecs ScanDrivers();

#if !__WIN32__
    private partial DriversSpecs ScanDrivers() => new();
#endif
}
