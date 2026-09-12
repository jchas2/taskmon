namespace Task.Monitor.System.Services.Disk;

public sealed class DiskInfo
{
    public DiskSpecs Specs { get; set; } = new();
    public DiskMetrics Metrics { get; set; } = new();
}
