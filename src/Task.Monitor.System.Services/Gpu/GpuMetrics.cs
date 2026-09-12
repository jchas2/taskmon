namespace Task.Monitor.System.Services.Gpu;

public class GpuMetrics
{
    public int    GpuCores           { get; set; }
    public double GpuPercentTime     { get; set; }

    // The per pid projection of the same \GPU Engine(*) counter array GpuPercentTime is derived
    // from, keyed by pid and expressed as a ratio like GpuPercentTime. Published here rather than
    // sampled again by ProcessService so both figures come from one collection of the provider and
    // can never disagree, and so the provider is only enumerated once per tick.
    public Dictionary<int, double> ProcessPercentTime { get; set; } = new();

    // Dedicated VRAM on the adapter. Task Manager's "Dedicated GPU memory".
    public long   TotalGpuMemory     { get; set; }
    public long   AvailableGpuMemory { get; set; }

    // Host memory the adapter can address through its apertures, capped by the OS at half of
    // physical RAM. Task Manager's "Shared GPU memory".
    public long   TotalSharedGpuMemory     { get; set; }
    public long   AvailableSharedGpuMemory { get; set; }

    // Dedicated plus shared. Task Manager's "GPU Memory".
    public long   TotalCombinedGpuMemory     { get; set; }
    public long   AvailableCombinedGpuMemory { get; set; }

    // Per adapter breakdown of the figures above, keyed to GpuDevice.AdapterLuid. Empty on a tick
    // where the adapters could not be queried, and off Windows.
    public List<GpuDeviceMetrics> Devices { get; set; } = new();
}
