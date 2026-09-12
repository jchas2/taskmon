using System.Collections.Concurrent;
using System.Diagnostics;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.DiskSpace;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.InstalledApps;
using Task.Monitor.System.Services.Memory;
using Task.Monitor.System.Services.Network;
using Task.Monitor.System.Services.Power;
using Task.Monitor.System.Services.Process;
using Task.Monitor.System.Services.Startup;
using Task.Monitor.System.Services.Thermal;
using Task.Monitor.System.Services.WindowsServices;

namespace Task.Monitor.System.Services;

public sealed class ServiceController : WorkerService
{
    // Owns all services and uses a single clock that publishes a snapshot of all system metrics.
    // Each service runs its own sampling thread and updates a system metric slot. 
    // Subscribers receive a single SystemSnapshotEventArgs at consistent update intervals.    
    private readonly List<ISystemService> allServices = new();
    private readonly ConcurrentDictionary<Type, object> systemMetrics = new();
    private readonly DeviceChangeNotifier deviceChangeNotifier = new();

    private long sequence;
    public event EventHandler<SystemSnapshotEventArgs>? SystemSnapshotUpdated;

    public ServiceController AddService(Func<ISystemService> serviceFactory)
    {
        ISystemService service = serviceFactory();

        if (allServices.Any(srv => srv.GetType() == service.GetType())) {
            throw new global::System.InvalidOperationException($"Service '{service.GetType()}' is already registered.");
        }

        if (service is WorkerService workerService) {
            workerService.Controller = this;
        }

        allServices.Add(service);
        return this;
    }

    // Returns the service itself, typed, so a caller that needs to change a setting on it can do
    // so without a cast. Reading what a service produced still goes through the published snapshot;
    // this is for the settings that are inputs to the sampling, not outputs of it.
    public T GetService<T>() where T : ISystemService =>
        (T)allServices.Single(srv => srv.GetType() == typeof(T));

    // The one interval for the whole app: every registered service samples at it, and this
    // controller publishes at it. Kept equal on purpose. Publishing faster than the services sample
    // re-emits identical snapshots and costs a redraw for no new data; publishing slower drops
    // samples the ui never sees.
    //
    // Applies to the services registered when it is called, so call it after the AddService chain.
    // Safe to call while running: each service picks the new interval up on its next wait.
    public void SetSamplingDelay(int delayInMilliseconds)
    {
        Delay = delayInMilliseconds;

        foreach (ISystemService systemService in allServices) {
            if (systemService is WorkerService workerService) {
                workerService.Delay = delayInMilliseconds;
            }
        }
    }

    private SystemSnapshot BuildSnapshot() =>
        new() {
            Sequence     = Interlocked.Increment(ref sequence),
            TimestampUtc = DateTime.UtcNow,
            Cpu          = GetLatestInfo<CpuInfo>(),
            Memory       = GetLatestInfo<MemoryInfo>(),
            Gpu          = GetLatestInfo<GpuInfo>(),
            Disk         = GetLatestInfo<DiskInfo>(),
            DiskSpace    = GetLatestInfo<DiskSpaceInfo>(),
            Network      = GetLatestInfo<NetworkInfo>(),
            Processes    = GetLatestInfo<ProcessInfo>(),
            Startup      = GetLatestInfo<StartupInfo>(),
            InstalledApps = GetLatestInfo<InstalledAppsInfo>(),
            WindowsServices = GetLatestInfo<WindowsServicesInfo>(),
            Thermal      = GetLatestInfo<ThermalInfo>(),
            Power        = GetLatestInfo<PowerInfo>(),
            Services     = BuildServiceHealth()
        };

    private ServiceHealth[] BuildServiceHealth()
    {
        var health = new ServiceHealth[allServices.Count];

        for (int i = 0; i < allServices.Count; i++) {
            ISystemService service = allServices[i];
            health[i] = new ServiceHealth(ShortName(service.GetType().Name), service.Status);
        }

        return health;
    }

    private static string ShortName(string typeName) =>
        typeName.EndsWith("Service", global::System.StringComparison.Ordinal)
            ? typeName[..^"Service".Length]
            : typeName;

    internal T? GetLatestInfo<T>() where T : class =>
        systemMetrics.TryGetValue(typeof(T), out object? sample)
            ? sample as T
            : null;

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        if (systemMetrics.IsEmpty) {
            return;
        }

        try {
            SystemSnapshot snapshot = BuildSnapshot();
            SystemSnapshotUpdated?.Invoke(this, new SystemSnapshotEventArgs(snapshot));
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex);
        }
    }

    internal void Store(Type infoType, object info) => systemMetrics[infoType] = info;

    // A plug and play device arrived or left: wake the service that owns that hardware so it
    // re-enumerates now instead of on its next poll boundary.
    private void OnDeviceChanged(DeviceCategory category)
    {
        switch (category) {
            case DeviceCategory.Storage:
                RequestImmediateRefresh<DiskService>();
                break;

            case DeviceCategory.Network:
                RequestImmediateRefresh<NetworkService>();
                break;

            case DeviceCategory.Gpu:
                RequestImmediateRefresh<GpuService>();
                break;
        }
    }

    private void RequestImmediateRefresh<T>() where T : ISystemService
    {
        foreach (ISystemService systemService in allServices) {
            if (systemService is T && systemService is WorkerService workerService) {
                workerService.RequestImmediateRefresh();
            }
        }
    }

    public override void Start()
    {
        deviceChangeNotifier.DeviceChanged += OnDeviceChanged;
        deviceChangeNotifier.Start();

        foreach (ISystemService systemService in allServices) {
            systemService.Start();
            Trace.WriteLine($"{systemService.GetType()} started, status: {systemService.Status}");
        }

        base.Start();
    }

    public override void Stop()
    {
        deviceChangeNotifier.DeviceChanged -= OnDeviceChanged;
        deviceChangeNotifier.Dispose();

        base.Stop();

        foreach (ISystemService systemService in allServices) {
            systemService.Stop();
            Trace.WriteLine($"{systemService.GetType()} stopped, status: {systemService.Status}");
        }
    }
}
