namespace Task.Monitor.System.Services.Gpu;

public class GpuMetrics
{
    public int    GpuCores                   { get; set; }
    public double GpuPercentTime             { get; set; }
    public Dictionary<int, double> ProcessPercentTime { get; set; } = new();
    public long   TotalGpuMemory             { get; set; }
    public long   AvailableGpuMemory         { get; set; }
    public long   TotalSharedGpuMemory       { get; set; }
    public long   AvailableSharedGpuMemory   { get; set; }
    public long   TotalCombinedGpuMemory     { get; set; }
    public long   AvailableCombinedGpuMemory { get; set; }
    public List<GpuDeviceMetrics> Devices    { get; set; } = new();
}
