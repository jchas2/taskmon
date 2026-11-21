namespace Task.Monitor.System.Services.Disk;

public sealed class DiskMetrics
{
    public ulong  TotalBytesRead           { get; set; }
    public ulong  TotalBytesWritten        { get; set; }
    public double ReadBytesPerSecond       { get; set; }
    public double WriteBytesPerSecond      { get; set; }
    public double PercentActiveTime        { get; set; }
    public double? LatencyMs               { get; set; }
    public double? QueueLength             { get; set; }
    public List<DiskDeviceMetrics> Devices { get; set; } = new();
}
