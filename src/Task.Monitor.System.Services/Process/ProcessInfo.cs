namespace Task.Monitor.System.Services.Process;

public sealed class ProcessInfo
{
    public ProcessSpecs Specs { get; set; } = new();
    public ProcessMetrics Metrics { get; set; } = new();
}
