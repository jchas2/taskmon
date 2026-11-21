using System.Diagnostics;

namespace Task.Monitor.System.Services.Disk;

public sealed partial class DiskService : WorkerService
{
    private const double BytesPerMegabyte = 1024.0 * 1024.0;
    private const int    MaxMissedSamples = 3;

    // Keyed by the counter's instance name: the BSD name on macOS ("disk0"), the PDH instance on
    // Windows ("0 C:", or "_Total").
    private readonly Dictionary<string, DiskState> states = new();

    private DiskSpecs diskSpecs = new();

    private sealed class DiskState
    {
        public int Index;

        public long PreviousReadBytes;
        public long PreviousWriteBytes;

        public long PreviousTime;
        public long PreviousOperations { get; set; }

        public long PreviousTimestamp;
        public int  ConsecutiveMisses;

        public ulong   TotalBytesRead;
        public ulong   TotalBytesWritten;
        public double  ReadBytesPerSecond;
        public double  WriteBytesPerSecond;
        public double  PercentActiveTime;
        public double? LatencyMs { get; set; }
    }

    private void BuildMetrics(DiskMetrics metrics)
    {
        foreach ((string name, DiskState state) in states) {
            if (state.Index < 0) {
                continue;
            }

            metrics.Devices.Add(new DiskDeviceMetrics {
                Index               = state.Index,
                InstanceName        = name,
                TotalBytesRead      = state.TotalBytesRead,
                TotalBytesWritten   = state.TotalBytesWritten,
                ReadBytesPerSecond  = state.ReadBytesPerSecond,
                WriteBytesPerSecond = state.WriteBytesPerSecond,
                PercentActiveTime   = state.PercentActiveTime,
                LatencyMs           = state.LatencyMs
            });

            metrics.TotalBytesRead      += state.TotalBytesRead;
            metrics.TotalBytesWritten   += state.TotalBytesWritten;
            metrics.ReadBytesPerSecond  += state.ReadBytesPerSecond;
            metrics.WriteBytesPerSecond += state.WriteBytesPerSecond;
            metrics.PercentActiveTime    = Math.Max(metrics.PercentActiveTime, state.PercentActiveTime);

            if (state.LatencyMs is { } latency) {
                metrics.LatencyMs = Math.Max(metrics.LatencyMs ?? 0.0, latency);
            }
        }

        metrics.Devices.Sort((left, right) => left.Index.CompareTo(right.Index));
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
        // Handles new drives inserted or ejected.
        if (ConsumeRefreshRequest()) {
            DiskSpecs refreshedSpecs = new();
            OnStartDiskSpecs(refreshedSpecs);
            LogSpecs(refreshedSpecs);
            diskSpecs = refreshedSpecs;
        }

        DiskInfo diskInfo = new() { Specs = diskSpecs };

        OnDoWorkDiskMetrics(diskInfo);
        Publish(diskInfo);
    }

    protected override void OnStart()
    {
        OnStartDiskSpecs(diskSpecs);
        LogSpecs(diskSpecs);
        OnStartDiskMetrics();
    }

    partial void OnStartDiskMetrics();

    protected override void OnStop()
    {
        OnStopDiskMetrics();
        states.Clear();
    }

    private void PruneMissing(Func<string, bool> isLive)
    {
        foreach ((string name, DiskState state) in states) {
            if (isLive(name)) {
                state.ConsecutiveMisses = 0;
            }
            else if (++state.ConsecutiveMisses >= MaxMissedSamples) {
                // A disk missing from a few samples in a row has gone.
                states.Remove(name);
            }
        }
    }
}
