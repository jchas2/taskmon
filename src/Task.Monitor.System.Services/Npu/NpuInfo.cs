namespace Task.Monitor.System.Services.Npu;

public sealed class NpuInfo
{
    public NpuSpecs Specs     { get; set; } = new();
    public NpuMetrics Metrics { get; set; } = new();
}
