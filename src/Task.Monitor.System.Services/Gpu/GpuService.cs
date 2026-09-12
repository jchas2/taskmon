namespace Task.Monitor.System.Services.Gpu;

public sealed partial class GpuService : WorkerService
{
    private GpuSpecs gpuSpecs = new();

    protected override void OnStart()
    {
        OnStartGpuSpecs(gpuSpecs);
        OnStartGpuPidMetrics();
    }

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        // A display adapter arrived or left (an eGPU over Thunderbolt, a hot-swapped card):
        // rebuild the adapter list. OnStartGpuSpecs appends, so it runs against a fresh GpuSpecs.
        if (ConsumeRefreshRequest()) {
            GpuSpecs refreshed = new();
            OnStartGpuSpecs(refreshed);
            gpuSpecs = refreshed;
        }

        GpuInfo gpuInfo = new();
        gpuInfo.Specs = gpuSpecs;
        gpuInfo.Metrics.GpuCores = gpuSpecs.GpuCores;
        gpuInfo.Metrics.TotalGpuMemory = gpuSpecs.TotalGpuMemory;
        
        OnDoWorkGpuPidMetrics(gpuInfo);
        OnDoWorkGpuMemoryMetrics(gpuInfo);
        Publish(gpuInfo);
    }

    protected override void OnStop()
    {
        OnStopGpuPidMetrics();
    }
}