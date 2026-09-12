namespace Task.Monitor.System.Services.Disk;

public sealed class DiskSpecs
{
    public List<DiskDevice> Devices { get; set; } = new();

    // Volumes that resolve to no physical extent on this machine: network mounts, RAM disks,
    // anything IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS declines. Kept rather than discarded so the
    // enumerated volumes still reconcile against what mountvol reports.
    public List<DiskVolume> UnattachedVolumes { get; set; } = new();
}
