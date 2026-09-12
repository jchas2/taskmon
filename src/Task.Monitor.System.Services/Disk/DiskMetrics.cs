namespace Task.Monitor.System.Services.Disk;

public sealed class DiskMetrics
{
    // Aggregate across every physical disk, taken from the PhysicalDisk "_Total" instance.
    // Totals accumulate from per cycle deltas rather than from an absolute baseline, so a disk
    // appearing or disappearing mid run cannot introduce a step change.
    public ulong  TotalBytesRead          { get; set; }
    public ulong  TotalBytesWritten       { get; set; }

    public double ReadBytesPerSecond      { get; set; }
    public double WriteBytesPerSecond     { get; set; }

    // Megabytes here are 1024 based, matching how Task Manager formats its KB/s readouts.
    public double ReadMegabytesPerSecond  { get; set; }
    public double WriteMegabytesPerSecond { get; set; }

    // Busiest single disk rather than the sum across disks. Two disks at 50% is a machine at 50%,
    // not 100%, and the "_Total" instance of % Idle Time is not bounded to a single disk's range.
    public double PercentActiveTime       { get; set; }

    public List<DiskDeviceMetrics> Devices { get; set; } = new();
}
