using Task.Monitor.Cli.Utils;
using Task.Monitor.System.Services.Cpu;
using Task.Monitor.System.Services.Disk;
using Task.Monitor.System.Services.Gpu;
using Task.Monitor.System.Services.Memory;
using Task.Monitor.System.Services.Network;
using Task.Monitor.System.Services.Process;
using Task.Monitor.System.Services.Soc;
using Task.Monitor.System.Services.Thermal;

namespace Task.Monitor.System.Services.Health;

// Runs the evaluator on the latest published metrics. A service rather than part of the view, so
// the status and its history keep building while another view is open.
public sealed class HealthService : WorkerService
{
    private const string MacDataVolume = "/System/Volumes/Data";
    private const string MacRootVolume = "/";

    private static readonly TimeSpan VolumeReadInterval = TimeSpan.FromSeconds(30);

    private readonly HealthEvaluator evaluator = new();

    private volatile HealthThresholds thresholds = new();

    private IReadOnlyList<HealthVolume> volumes = [];
    private DateTime                    nextVolumeReadUtc;

    private static bool IsSystemVolume(string name) =>
        name is MacRootVolume or MacDataVolume ||
        string.Equals(Path.GetPathRoot(Environment.SystemDirectory), name, StringComparison.OrdinalIgnoreCase);

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        DateTime utcNow = DateTime.UtcNow;

        if (utcNow >= nextVolumeReadUtc) {
            volumes           = ReadVolumes();
            nextVolumeReadUtc = utcNow + VolumeReadInterval;
        }

        HealthSample sample = new() {
            Cpu       = GetLatest<CpuInfo>(),
            Memory    = GetLatest<MemoryInfo>(),
            Gpu       = GetLatest<GpuInfo>(),
            Disk      = GetLatest<DiskInfo>(),
            Network   = GetLatest<NetworkInfo>(),
            Processes = GetLatest<ProcessInfo>(),
            Thermal   = GetLatest<ThermalInfo>(),
            Soc       = GetLatest<SocInfo>(),
            Volumes   = volumes
        };

        Publish(evaluator.Evaluate(sample, thresholds, utcNow));
    }

    // Read here because the free space on DiskVolume is only taken when the disks are enumerated.
    // A volume with nothing free is skipped unless it's the system volume, as that's what a mounted
    // read-only image looks like.
    internal static IReadOnlyList<HealthVolume> ReadVolumes()
    {
        List<HealthVolume> volumes = [];

        foreach (DriveInfo drive in DriveInfo.GetDrives()) {
            try {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady || drive.TotalSize <= 0) {
                    continue;
                }

                bool isSystem = IsSystemVolume(drive.Name);

                if (drive.AvailableFreeSpace > 0 || isSystem) {
                    volumes.Add(new HealthVolume(drive.Name, drive.AvailableFreeSpace, drive.TotalSize, isSystem));
                }
            }
            catch (Exception ex) {
                // Some special mounts throw on property access; skip them.
                TraceEx.WriteLineOnce(drive.Name, $"Skipping volume {drive.Name}: {ex.Message}");
            }
        }

        return volumes;
    }

    public HealthThresholds Thresholds
    {
        get => thresholds;
        set => thresholds = value;
    }
}
