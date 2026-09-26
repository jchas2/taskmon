namespace Task.Monitor.System.Services.Gpu;

public sealed class GpuSpecs
{
    public List<GpuDevice> Devices { get; set; } = new();
    public int  GpuCores           { get; set; }
    public long TotalGpuMemory     { get; set; }
}
