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

public sealed partial class HealthService : WorkerService
{
    private const string MacDataVolume = "/System/Volumes/Data";
    private const string MacRootVolume = "/";

    private static readonly TimeSpan ReportReadInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan VolumeReadInterval = TimeSpan.FromSeconds(30);

    private readonly HealthEvaluator evaluator      = new();
    private readonly HungAppTracker  hungAppTracker = new();

    private volatile HealthThresholds thresholds = new();

    private IReadOnlyList<DriveHealth>  drives  = [];
    private IReadOnlyList<HealthReport> reports = [];
    private IReadOnlyList<HealthVolume> volumes = [];
    private DateTime                    nextReportReadUtc;
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

        DiskInfo?    disks     = GetLatest<DiskInfo>();
        ProcessInfo? processes = GetLatest<ProcessInfo>();

        if (utcNow >= nextReportReadUtc) {
            reports           = ReadReports(thresholds.ReportLookbackHours, thresholds.EventHistorySize);
            drives            = ReadDrives(disks);
            nextReportReadUtc = utcNow + ReportReadInterval;
        }

        HealthSample sample = new() {
            Cpu               = GetLatest<CpuInfo>(),
            Memory            = GetLatest<MemoryInfo>(),
            Gpu               = GetLatest<GpuInfo>(),
            Disk              = disks,
            Network           = GetLatest<NetworkInfo>(),
            Processes         = processes,
            Thermal           = GetLatest<ThermalInfo>(),
            Soc               = GetLatest<SocInfo>(),
            Volumes           = volumes,
            CanDetectHungApps = CanDetectHungApps,
            HungApps          = hungAppTracker.Update(ReadHungProcessIds(), pid => ProcessName(processes, pid), utcNow),
            Drives            = drives,
            Reports           = reports
        };

        Publish(evaluator.Evaluate(sample, thresholds, utcNow));
    }

    private static string ProcessName(ProcessInfo? processes, int pid) =>
        processes?.Metrics.Entries.FirstOrDefault(entry => entry.Pid == pid)?.ProcessName ?? $"Process {pid}";

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
