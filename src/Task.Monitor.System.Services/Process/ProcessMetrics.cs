namespace Task.Monitor.System.Services.Process;

public sealed class ProcessMetrics
{
    public int  ProcessCount { get; set; }
    public int  ThreadCount  { get; set; }
    public int  RunningCount { get; set; }
    public uint HandleCount  { get; set; }

    public List<ProcessEntry> Entries { get; set; } = new();
}
