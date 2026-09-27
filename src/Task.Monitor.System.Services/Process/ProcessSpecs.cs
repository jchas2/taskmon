namespace Task.Monitor.System.Services.Process;

public sealed class ProcessSpecs
{
    public int LogicalProcessorCount { get; set; } = Environment.ProcessorCount;
    public bool IrixMode { get; set; }
}
