using System.Diagnostics;
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

    private readonly HealthEvaluator    evaluator      = new();
    private readonly HungAppTracker     hungAppTracker = new();
    private readonly VolumeTrendTracker volumeTrends   = new();

    private volatile HealthThresholds thresholds = new();

    private IReadOnlyList<DriveHealth>  drives  = [];
    private IReadOnlyList<HealthReport> reports = [];
    private IReadOnlyList<HealthVolume> volumes = [];
    private DateTime                    nextReportReadUtc;
    private DateTime                    nextVolumeReadUtc;
    private bool                        eventsRestored;

    public HealthEventStore? EventStore { get; set; }

    private static bool IsSystemVolume(string name) =>
        name is MacRootVolume or MacDataVolume ||
        string.Equals(Path.GetPathRoot(Environment.SystemDirectory), name, StringComparison.OrdinalIgnoreCase);

    internal static bool IsTracked(bool isSystem, bool isReadOnly) => isSystem || !isReadOnly;

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        DateTime utcNow = DateTime.UtcNow;

        if (utcNow >= nextVolumeReadUtc) {
            volumes           = volumeTrends.Update(ReadVolumes(), utcNow);
            nextVolumeReadUtc = utcNow + VolumeReadInterval;
        }

        DiskInfo?    disks     = GetLatest<DiskInfo>();
        ProcessInfo? processes = GetLatest<ProcessInfo>();

        if (utcNow >= nextReportReadUtc) {
            reports           = ReadReports(thresholds.ReportLookbackHours, thresholds.EventHistorySize);
            drives            = ReadDrives(disks);
            nextReportReadUtc = utcNow + ReportReadInterval;

            if (IsFirstCycle) {
                Trace.WriteLine($"{ServiceName}: {reports.Count} crash and hang reports in the last {thresholds.ReportLookbackHours} hours");
            }
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
        SaveEvents(evaluator.TakeChanges());
    }

    protected override void OnStart()
    {
        if (eventsRestored) {
            return;
        }

        if (EventStore is not { } store || thresholds.EventHistorySize == 0) {
            Trace.WriteLine(EventStore is null
                ? $"{ServiceName}: event history SKIPPED, no events folder"
                : $"{ServiceName}: event history SKIPPED, turned off");

            return;
        }

        eventsRestored = true;

        IReadOnlyList<HealthEvent> restored = store.Load(thresholds.EventHistorySize);

        evaluator.Restore(restored);
        store.Prune(thresholds.EventHistorySize);

        Trace.WriteLine($"{ServiceName}: event history OK, {restored.Count} events restored");
    }

    protected override void OnStop() => SaveEvents(evaluator.Finish(DateTime.UtcNow));

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

                if (IsTracked(isSystem, IsReadOnlyVolume(drive.Name))) {
                    volumes.Add(new HealthVolume(drive.Name, drive.AvailableFreeSpace, drive.TotalSize, isSystem));
                }
            }
            catch (Exception ex) {
                TraceEx.WriteLineOnce(drive.Name, $"Skipping volume {drive.Name}: {ex.Message}");
            }
        }

        return volumes;
    }

    private void SaveEvents(IReadOnlyList<HealthEvent> events)
    {
        if (events.Count == 0 || EventStore is not { } store || thresholds.EventHistorySize == 0) {
            return;
        }

        try {
            foreach (HealthEvent healthEvent in events) {
                store.Save(healthEvent);
            }

            store.Prune(thresholds.EventHistorySize);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            TraceEx.WriteLineOnce(nameof(SaveEvents), $"Skipping saving health events: {ex.Message}");
        }
    }

    public HealthThresholds Thresholds
    {
        get => thresholds;
        set => thresholds = value;
    }
}
