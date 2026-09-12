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

public sealed record SystemSnapshot
{
    public long Sequence { get; init; }
    public DateTime TimestampUtc { get; init; }
    public CpuInfo? Cpu { get; init; }
    public MemoryInfo? Memory { get; init; }
    public GpuInfo? Gpu { get; init; }
    public DiskInfo? Disk { get; init; }
    public DiskSpaceInfo? DiskSpace { get; init; }
    public NetworkInfo? Network { get; init; }
    public ProcessInfo? Processes { get; init; }
    public StartupInfo? Startup { get; init; }
    public InstalledAppsInfo? InstalledApps { get; init; }
    public ThermalInfo? Thermal { get; init; }
    public PowerInfo? Power { get; init; }

    // Windows services (daemons) - distinct from Services below, which is this app's own worker
    // services.
    public WindowsServicesInfo? WindowsServices { get; init; }

    public IReadOnlyList<ServiceHealth> Services { get; init; } = [];
}
