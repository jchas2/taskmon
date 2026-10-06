namespace Task.Monitor.System.Services.Disk;

public sealed class DiskVolume
{
    public string   VolumeName         { get; set; } = string.Empty;
    public string[] MountPoints        { get; set; } = [];
    public string   Label              { get; set; } = string.Empty;
    public string   FileSystem         { get; set; } = string.Empty;
    public uint     SerialNumber       { get; set; }
    public string   DriveType          { get; set; } = Constants.NotAvailable;

    public bool     IsReady            { get; set; }

    public long     FormattedCapacity  { get; set; }
    public long     AvailableFreeSpace { get; set; }
    public double   UsedRatio          { get; set; }
}
