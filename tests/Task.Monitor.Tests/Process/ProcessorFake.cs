using Task.Monitor.Process;
using Task.Monitor.System;

namespace Task.Monitor.Tests.Process;

public sealed class ProcessorFake : IProcessor
{
    private SystemStatistics statistics;
    private List<ProcessorInfo> procInfos = []; 
    
    public event EventHandler<ProcessorEventArgs>? ProcessorUpdated;

    public void AddProcessorInfos(List<ProcessorInfo> procInfos) =>
        this.procInfos = procInfos;
    
    public void AddSystemStats(SystemStatistics statistics) =>
        this.statistics = statistics;
    
    public int Delay { get; set; }
    
    public bool IrixMode { get; set; }
    
    public bool IsRunning => false;

    public int IterationLimit { get; set; }
    
    public int ProcessCount { get; }

    public void RaiseProcessorUpdatedEvent()
    {
        if (ProcessorUpdated != null) {
            ProcessorUpdated(this, new ProcessorEventArgs(procInfos, statistics));
        }
    }

    public void Run() { }

    public void Stop() { }

    public int ThreadCount { get; }
}
