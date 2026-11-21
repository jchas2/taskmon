using System.Collections.Concurrent;
using System.Diagnostics;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.DiskSpace;
using Task.Monitor.System.Services.Drivers;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Health;
using Task.Monitor.System.Services.InstalledApps;
using Task.Monitor.System.Services.LaunchdServices;
using Task.Monitor.System.Services.Memory;
using Task.Monitor.System.Services.Network;
using Task.Monitor.System.Services.Npu;
using Task.Monitor.System.Services.Power;
using Task.Monitor.System.Services.Process;
using Task.Monitor.System.Services.Soc;
using Task.Monitor.System.Services.Startup;
using Task.Monitor.System.Services.Thermal;
using Task.Monitor.System.Services.WindowsServices;

namespace Task.Monitor.System.Services;

public sealed class ServiceController
{
    private readonly List<ISystemService>               allServices          = new();
    private readonly ConcurrentDictionary<Type, object> systemMetrics        = new();
    private readonly DeviceChangeNotifier               deviceChangeNotifier = new();
    private readonly SnapshotLoop                       snapshotLoop;

    private long sequence;

    private sealed class SnapshotLoop(ServiceController controller) : WorkerService
    {
        protected override void OnDoWork(CancellationToken cancellationToken) => controller.PublishSnapshot();
    }

    public event EventHandler<SystemSnapshotEventArgs>? SystemSnapshotUpdated;

    public ServiceController() => snapshotLoop = new SnapshotLoop(this);

    public ServiceController AddService(ISystemService service)
    {
        if (allServices.Any(srv => srv.GetType() == service.GetType())) {
            throw new InvalidOperationException($"Service '{service.GetType()}' is already registered.");
        }

        if (service is WorkerService workerService) {
            workerService.Controller = this;
        }

        allServices.Add(service);
        return this;
    }

    private ServiceHealth[] BuildServiceHealth()
    {
        var health = new ServiceHealth[allServices.Count];

        for (int i = 0; i < allServices.Count; i++) {
            ISystemService service = allServices[i];
            health[i] = new ServiceHealth(ShortName(service.GetType().Name), service.Status);
        }

        return health;
    }

    private SystemSnapshot BuildSnapshot() =>
        new() {
            Sequence        = Interlocked.Increment(ref sequence),
            TimestampUtc    = DateTime.UtcNow,
            Cpu             = GetLatestInfo<CpuInfo>(),
            Memory          = GetLatestInfo<MemoryInfo>(),
            Gpu             = GetLatestInfo<GpuInfo>(),
            Npu             = GetLatestInfo<NpuInfo>(),
            Disk            = GetLatestInfo<DiskInfo>(),
            DiskSpace       = GetLatestInfo<DiskSpaceInfo>(),
            Network         = GetLatestInfo<NetworkInfo>(),
            Processes       = GetLatestInfo<ProcessInfo>(),
            Startup         = GetLatestInfo<StartupInfo>(),
            InstalledApps   = GetLatestInfo<InstalledAppsInfo>(),
            WindowsServices = GetLatestInfo<WindowsServicesInfo>(),
            LaunchdServices = GetLatestInfo<LaunchdServicesInfo>(),
            Drivers         = GetLatestInfo<DriversInfo>(),
            Thermal         = GetLatestInfo<ThermalInfo>(),
            Power           = GetLatestInfo<PowerInfo>(),
            Soc             = GetLatestInfo<SocInfo>(),
            Health          = GetLatestInfo<HealthInfo>(),
            Services        = BuildServiceHealth()
        };

    public int Delay
    {
        get => snapshotLoop.Delay;
        set => snapshotLoop.Delay = value;
    }

    internal T? GetLatestInfo<T>() where T : class =>
        systemMetrics.TryGetValue(typeof(T), out object? sample)
            ? sample as T
            : null;

    public T GetService<T>() where T : ISystemService =>
        allServices.OfType<T>().Single();

    public IReadOnlyList<ServiceHealth> GetServiceHealth() => BuildServiceHealth();

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

    private void PublishSnapshot()
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

    internal void RequestImmediateRefresh<T>() where T : ISystemService
    {
        foreach (ISystemService systemService in allServices) {
            if (systemService is T && systemService is WorkerService workerService) {
                workerService.RequestImmediateRefresh();
            }
        }
    }

    public void SetSamplingDelay(int delayInMilliseconds)
    {
        Delay = delayInMilliseconds;

        foreach (ISystemService systemService in allServices) {
            if (systemService is WorkerService workerService) {
                workerService.Delay = delayInMilliseconds;
            }
        }
    }

    private static string ShortName(string typeName) =>
        typeName.EndsWith("Service", global::System.StringComparison.Ordinal)
            ? typeName[..^"Service".Length]
            : typeName;

    public void Start()
    {
        deviceChangeNotifier.DeviceChanged += OnDeviceChanged;
        deviceChangeNotifier.Start();

        foreach (ISystemService systemService in allServices) {
            systemService.Start();
        }

        snapshotLoop.Start();
    }

    public void Stop()
    {
        deviceChangeNotifier.DeviceChanged -= OnDeviceChanged;
        deviceChangeNotifier.Dispose();

        snapshotLoop.Stop();

        foreach (ISystemService systemService in allServices) {
            systemService.Stop();
            Trace.WriteLine($"{systemService.GetType().Name}: stopped, status {systemService.Status}");
        }
    }

    internal void Store(Type infoType, object info) => systemMetrics[infoType] = info;
}
