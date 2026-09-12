namespace Task.Monitor.System.Services.Disk;

// A formatted volume hosted on one or more physical disks. Enumerated by volume rather than by
// drive letter: an EFI system partition, an MSR and a recovery partition all have no mount point
// at all, and letter based enumeration silently loses them along with their share of the disk.
public sealed class DiskVolume
{
    public string   VolumeName         { get; set; } = string.Empty;
    public string[] MountPoints        { get; set; } = [];
    public string   Label              { get; set; } = string.Empty;
    public string   FileSystem         { get; set; } = string.Empty;
    public uint     SerialNumber       { get; set; }
    public string   DriveType          { get; set; } = DiskDeviceParser.NotAvailable;

    // False when GetVolumeInformationW declines to answer: an empty card reader, an unformatted
    // RAW volume, a disconnected network mount.
    public bool     IsReady            { get; set; }

    public long     FormattedCapacity  { get; set; }
    public long     AvailableFreeSpace { get; set; }
    public double   UsedRatio          { get; set; }
}
