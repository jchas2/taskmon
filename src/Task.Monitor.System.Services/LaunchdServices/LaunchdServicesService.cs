namespace Task.Monitor.System.Services.LaunchdServices;

public sealed partial class LaunchdServicesService : WorkerService
{
    private const int RescanEveryCycles = 40;

    private LaunchdServicesSpecs servicesSpecs = new();

    private int cyclesSinceScan;

    private partial IEnumerable<LaunchdServiceInfo> EnumerateServices();

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        if (ConsumeRefreshRequest() || ++cyclesSinceScan >= RescanEveryCycles) {
            servicesSpecs   = ScanServices();
            cyclesSinceScan = 0;
        }

        Publish(new LaunchdServicesInfo { Specs = servicesSpecs });
    }

    protected override void OnStart() =>
        servicesSpecs = ScanServices();

    private LaunchdServicesSpecs ScanServices()
    {
        LaunchdServicesSpecs specs = new();

        specs.Services.AddRange(EnumerateServices());

        specs.Services.Sort(static (left, right) => {
            int byGroup = string.Compare(left.Group, right.Group, StringComparison.OrdinalIgnoreCase);

            return byGroup != 0
                ? byGroup
                : string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase);
        });

        return specs;
    }

#if !__APPLE__
    private partial IEnumerable<LaunchdServiceInfo> EnumerateServices() => [];
#endif
}
