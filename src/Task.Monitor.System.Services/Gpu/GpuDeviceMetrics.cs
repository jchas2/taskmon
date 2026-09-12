namespace Task.Monitor.System.Services.Gpu;

// Utilisation and memory for a single adapter, matched to GpuDevice.AdapterLuid. The headline
// GpuMetrics figures stay the busiest-engine and summed-memory view across every adapter; this is
// the per adapter breakdown behind them, mirroring DiskDeviceMetrics.
public sealed class GpuDeviceMetrics
{
    public int    Index                    { get; set; }
    public long   AdapterLuid              { get; set; }

    // 0.0 - 1.0, the busiest engine on this adapter, matching how GpuMetrics.GpuPercentTime is
    // derived for the machine as a whole.
    public double GpuPercentTime           { get; set; }

    public long   TotalGpuMemory           { get; set; }
    public long   AvailableGpuMemory       { get; set; }
    public long   TotalSharedGpuMemory     { get; set; }
    public long   AvailableSharedGpuMemory { get; set; }

    // Dedicated plus shared for this adapter, mirroring GpuMetrics.TotalCombinedGpuMemory.
    public long   TotalCombinedGpuMemory     { get; set; }
    public long   AvailableCombinedGpuMemory { get; set; }
}
