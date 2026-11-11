using System.Diagnostics;

namespace Task.Monitor.System.Services.Disk;

public sealed partial class DiskService : WorkerService
{
    private const int MaxMissedSamples = 3;

    private DiskSpecs diskSpecs = new();

    private interface IMissedSampleCounter
    {
        int ConsecutiveMisses { get; set; }
    }

    private void LogSpecs(DiskSpecs specs)
    {
        Trace.WriteLine($"{ServiceName}: {specs.Devices.Count} disks, {specs.UnattachedVolumes.Count} volumes on no disk");

        foreach (DiskDevice device in specs.Devices) {
            Trace.WriteLine(
                $"{ServiceName}: disk {device.Index} {device.Model}, {device.BusType}, {device.MediaType}, " +
                $"{device.Capacity / BytesPerMegabyte:0} MB{(device.IsRemovable ? ", removable" : string.Empty)}");

            foreach (DiskVolume volume in device.Volumes) {
                LogVolume($"disk {device.Index} volume", volume);
            }
        }

        foreach (DiskVolume volume in specs.UnattachedVolumes) {
            LogVolume("volume on no disk", volume);
        }
    }

    private void LogVolume(string owner, DiskVolume volume)
    {
        string mountPoint = volume.MountPoints.Length > 0 ? volume.MountPoints[0] : "without a mount point";

        Trace.WriteLine(volume.IsReady
            ? $"{ServiceName}: {owner} {mountPoint}, {volume.FileSystem}, {volume.FormattedCapacity / BytesPerMegabyte:0} MB, {volume.DriveType}"
            : $"{ServiceName}: {owner} {mountPoint}, not ready, {volume.DriveType}");
    }

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        // Caters for new drives inserted or ejected.
        if (ConsumeRefreshRequest()) {
            DiskSpecs refreshedSpecs = new();
            OnStartDiskSpecs(refreshedSpecs);
            LogSpecs(refreshedSpecs);
            diskSpecs = refreshedSpecs;
        }

        DiskInfo diskInfo = new();
        diskInfo.Specs = diskSpecs;

        OnDoWorkDiskMetrics(diskInfo);
        Publish(diskInfo);
    }

    protected override void OnStart()
    {
        OnStartDiskSpecs(diskSpecs);
        LogSpecs(diskSpecs);
        OnStartDiskMetrics();
    }

    protected override void OnStop()
    {
        OnStopDiskMetrics();
    }

    private static void PruneMissing<TState>(Dictionary<string, TState> states, Func<string, bool> isLive)
        where TState : class, IMissedSampleCounter
    {
        List<string>? stale = null;

        foreach ((string name, TState state) in states) {
            if (isLive(name)) {
                state.ConsecutiveMisses = 0;
                continue;
            }

            if (++state.ConsecutiveMisses >= MaxMissedSamples) {
                (stale ??= []).Add(name);
            }
        }

        if (stale is null) {
            return;
        }

        foreach (string name in stale) {
            states.Remove(name);
        }
    }
}
