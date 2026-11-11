using System.Diagnostics;

namespace Task.Monitor.System.Services.WindowsServices;

public sealed partial class WindowsServicesService : WorkerService
{
    private const int RescanEveryCycles = 40;

    private readonly ServiceControlQueue controlQueue = new();

    private WindowsServicesSpecs servicesSpecs = new();

    private int cyclesSinceScan;

    private partial ServiceControlOutcome ApplyControl(string serviceName, bool start);

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        bool rescan = controlQueue.Apply(ApplyControl, servicesSpecs, DateTime.UtcNow);

        rescan |= ConsumeRefreshRequest();

        if (rescan || ++cyclesSinceScan >= RescanEveryCycles) {
            servicesSpecs   = ScanServices();
            cyclesSinceScan = 0;
        }
        else if (controlQueue.IsFollowing) {
            UpdateStatuses(servicesSpecs, QueryStatuses());
        }

        controlQueue.Settle(servicesSpecs, DateTime.UtcNow);

        Publish(new WindowsServicesInfo { Specs = servicesSpecs });
    }

    protected override void OnStart()
    {
        servicesSpecs = ScanServices();
        Trace.WriteLine(servicesSpecs.Services.Count > 0
            ? $"{ServiceName}: {servicesSpecs.Services.Count} services"
            : $"{ServiceName}: services FAILED, none read, see the service manager error above");
    }

    private partial IReadOnlyList<(string ServiceName, WindowsServiceStatus Status)> QueryStatuses();

    public void RequestControl(string serviceName, bool start, Action<ServiceControlOutcome> completed)
    {
        controlQueue.Enqueue(serviceName, start, completed);
        Wake();
    }

    private partial WindowsServicesSpecs ScanServices();

    public static bool SupportsControl { get; } =
#if __WIN32__
        true;
#else
        false;
#endif

    internal static void UpdateStatuses(
        WindowsServicesSpecs                                             specs,
        IReadOnlyList<(string ServiceName, WindowsServiceStatus Status)> statuses)
    {
        Dictionary<string, WindowsServiceStatus> byName = new(StringComparer.OrdinalIgnoreCase);

        foreach ((string serviceName, WindowsServiceStatus status) in statuses) {
            byName[serviceName] = status;
        }

        foreach (WindowsServiceInfo service in specs.Services) {
            if (byName.TryGetValue(service.ServiceName, out WindowsServiceStatus status)) {
                service.Status = status;
            } 
        }
    }

#if !__WIN32__
    private partial ServiceControlOutcome ApplyControl(string serviceName, bool start) =>
        new(ServiceControlResult.NotSupported);

    private partial IReadOnlyList<(string ServiceName, WindowsServiceStatus Status)> QueryStatuses() => [];

    private partial WindowsServicesSpecs ScanServices() => new();
#endif
}
