namespace Task.Monitor.System.Services.Process;

public sealed class ProcessMetrics
{
    // Only the aggregates that are genuinely a property of the process list. System wide cpu,
    // memory, gpu, disk and network totals are deliberately absent: they belong to the services
    // that own those providers, and restating them here would be a second answer to the same
    // question sampled at a different instant.
    public int ProcessCount { get; set; }
    public int ThreadCount { get; set; }
    public int RunningCount { get; set; }
    public uint HandleCount { get; set; }

    public List<ProcessEntry> Entries { get; set; } = new();
}
