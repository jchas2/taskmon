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
        // Caters for swappable GPUs.
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