namespace Task.Monitor.System.Services.Memory;

public sealed partial class MemoryService : WorkerService
{
    private MemorySpecs memorySpecs = new();

    protected override void OnStart() =>
        OnStartMemorySpecs(memorySpecs);

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        MemoryInfo memoryInfo = new();
        memoryInfo.Specs = memorySpecs;
        
        OnDoWorkMemoryMetrics(memoryInfo);
        OnDoWorkMemoryCompressionMetrics(memoryInfo);
        Publish(memoryInfo);
    }

    protected override void OnStop() { }
}
