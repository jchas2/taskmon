namespace Task.Monitor.System.Services.Memory;

public sealed class MemoryInfo
{
    public MemorySpecs Specs { get; set; } = new();
    public MemoryMetrics Metrics { get; set; } = new();
}