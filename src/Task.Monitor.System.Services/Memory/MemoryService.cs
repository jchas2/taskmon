namespace Task.Monitor.System.Services.Memory;

public sealed partial class MemoryService : WorkerService
{
    private MemorySpecs memorySpecs = new();

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        MemoryInfo memoryInfo = new();
        memoryInfo.Specs = memorySpecs;
        
        OnDoWorkMemoryMetrics(memoryInfo);
        OnDoWorkMemoryCompressionMetrics(memoryInfo);
        OnDoWorkPressureMetrics(memoryInfo);
        Publish(memoryInfo);
    }

    protected override void OnStart()
    {
        OnStartMemorySpecs(memorySpecs);
        OnStartPressureMetrics();
    }

    protected override void OnStop() =>
        OnStopPressureMetrics();
}
