using System.Diagnostics;

namespace Task.Monitor.System.Services.WindowsServices;

public sealed partial class WindowsServicesService : WorkerService
{
    private const int RescanEveryCycles = 40;
    private const int StatusEveryCycles = 10;

    private readonly ServiceControlQueue controlQueue = new();

    private WindowsServicesSpecs servicesSpecs = new();

    private int cyclesSinceScan;
    private int cyclesSinceStatus;

    private partial ServiceControlOutcome ApplyControl(string serviceName, bool start);

    internal static Dictionary<int, WindowsServiceInfo> MapProcesses(IEnumerable<WindowsServiceInfo> services)
    {
        Dictionary<int, WindowsServiceInfo> map = new();

        foreach (WindowsServiceInfo service in services) {
            if (service.ProcessId != 0 &&
                service.Status is WindowsServiceStatus.Running
                               or WindowsServiceStatus.StartPending
                               or WindowsServiceStatus.StopPending
                               or WindowsServiceStatus.PausePending
                               or WindowsServiceStatus.Paused) {

                map[service.ProcessId] = service;
            }
        }

        return map;
    }

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        bool rescan = controlQueue.Apply(ApplyControl, servicesSpecs, DateTime.UtcNow);

        if (RescanDue(ref cyclesSinceScan, RescanEveryCycles) || rescan) {
            servicesSpecs     = ScanServices();
            cyclesSinceScan   = 0;
            cyclesSinceStatus = 0;
        }
        else if (controlQueue.IsFollowing || ++cyclesSinceStatus >= StatusEveryCycles) {
            UpdateStatuses(servicesSpecs, QueryStatuses());
            cyclesSinceStatus = 0;
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

    private partial IReadOnlyList<(string ServiceName, WindowsServiceStatus Status, int ProcessId)> QueryStatuses();

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
        WindowsServicesSpecs                                                            specs,
        IReadOnlyList<(string ServiceName, WindowsServiceStatus Status, int ProcessId)> statuses)
    {
        Dictionary<string, (WindowsServiceStatus Status, int ProcessId)> byName = new(StringComparer.OrdinalIgnoreCase);

        foreach ((string serviceName, WindowsServiceStatus status, int processId) in statuses) {
            byName[serviceName] = (status, processId);
        }

        foreach (WindowsServiceInfo service in specs.Services) {
            if (byName.TryGetValue(service.ServiceName, out (WindowsServiceStatus Status, int ProcessId) current)) {
                service.Status    = current.Status;
                service.ProcessId = current.ProcessId;
            }
        }
    }

#if !__WIN32__
    private partial ServiceControlOutcome ApplyControl(string serviceName, bool start) =>
        new(ServiceControlResult.NotSupported);

    private partial IReadOnlyList<(string ServiceName, WindowsServiceStatus Status, int ProcessId)> QueryStatuses() => [];

    private partial WindowsServicesSpecs ScanServices() => new();
#endif
}
