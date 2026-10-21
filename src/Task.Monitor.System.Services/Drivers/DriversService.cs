namespace Task.Monitor.System.Services.Drivers;

public sealed partial class DriversService : WorkerService
{
    private const int RescanEveryCycles = 40;

    private DriversSpecs driversSpecs = new();

    private int cyclesSinceScan;

    private partial IEnumerable<DriverInfo> EnumerateDrivers();

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        if (ConsumeRefreshRequest() || ++cyclesSinceScan >= RescanEveryCycles) {
            driversSpecs    = ScanDrivers();
            cyclesSinceScan = 0;
        }

        Publish(new DriversInfo { Specs = driversSpecs });
    }

    protected override void OnStart() =>
        driversSpecs = ScanDrivers();

    private DriversSpecs ScanDrivers()
    {
        DriversSpecs specs = new();

        specs.Drivers.AddRange(EnumerateDrivers());

        specs.Drivers.Sort(static (left, right) =>
            string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));

        return specs;
    }

#if !__WIN32__ && !__APPLE__
    private partial IEnumerable<DriverInfo> EnumerateDrivers() => [];
#endif
}
