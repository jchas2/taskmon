namespace Task.Monitor.System.Services.Gpu;

public sealed class GpuDeviceMetrics
{
    public int    Index                    { get; set; }
    public long   AdapterLuid              { get; set; }

    public double GpuPercentTime           { get; set; }

    public long   TotalGpuMemory           { get; set; }
    public long   AvailableGpuMemory       { get; set; }
    public long   TotalSharedGpuMemory     { get; set; }
    public long   AvailableSharedGpuMemory { get; set; }

    public long   TotalCombinedGpuMemory     { get; set; }
    public long   AvailableCombinedGpuMemory { get; set; }
}
