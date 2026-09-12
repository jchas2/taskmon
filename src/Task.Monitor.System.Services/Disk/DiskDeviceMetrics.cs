namespace Task.Monitor.System.Services.Disk;

// Throughput for a single physical disk, matched to DiskDevice.Index.
public sealed class DiskDeviceMetrics
{
    public int    Index                   { get; set; }
    public string InstanceName            { get; set; } = string.Empty;

    public ulong  TotalBytesRead          { get; set; }
    public ulong  TotalBytesWritten       { get; set; }

    public double ReadBytesPerSecond      { get; set; }
    public double WriteBytesPerSecond     { get; set; }
    public double ReadMegabytesPerSecond  { get; set; }
    public double WriteMegabytesPerSecond { get; set; }

    public double PercentActiveTime       { get; set; }
}
