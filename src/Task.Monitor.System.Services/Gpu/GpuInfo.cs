namespace Task.Monitor.System.Services.Gpu;

public class GpuInfo
{
    public GpuMetrics Metrics { get; set; } = new();
    public GpuSpecs   Specs   { get; set; } = new();
}
