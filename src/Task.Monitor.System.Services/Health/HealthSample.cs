using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Memory;
using Task.Monitor.System.Services.Network;
using Task.Monitor.System.Services.Process;
using Task.Monitor.System.Services.Soc;
using Task.Monitor.System.Services.Thermal;

namespace Task.Monitor.System.Services.Health;

public sealed record HealthSample
{
    public CpuInfo?                    Cpu       { get; init; }
    public MemoryInfo?                 Memory    { get; init; }
    public GpuInfo?                    Gpu       { get; init; }
    public DiskInfo?                   Disk      { get; init; }
    public NetworkInfo?                Network   { get; init; }
    public ProcessInfo?                Processes { get; init; }
    public ThermalInfo?                Thermal   { get; init; }
    public SocInfo?                    Soc       { get; init; }
    public IReadOnlyList<HealthVolume> Volumes   { get; init; } = [];

    public bool                        CanDetectHungApps { get; init; }
    public IReadOnlyList<HungApp>      HungApps          { get; init; } = [];
    public IReadOnlyList<DriveHealth>  Drives            { get; init; } = [];
    public IReadOnlyList<HealthReport> Reports           { get; init; } = [];
}
