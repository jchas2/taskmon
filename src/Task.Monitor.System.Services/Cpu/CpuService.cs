namespace Task.Monitor.System.Services.Cpu;

public sealed partial class CpuService : WorkerService
{
    private CpuSpecs cpuSpecs = new();
      
    protected override void OnStart()
    {   
        OnStartCpuSpecs(ref cpuSpecs);
        OnStartCpuCore();
    }

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        CpuInfo cpuInfo = new();
        cpuInfo.Specs = cpuSpecs;

        OnDoWorkCpuCore(cpuInfo);
        OnDoWorkCpuMetrics(cpuInfo);
        Publish(cpuInfo);
    }
    
    protected override void OnStop()
    {
        OnStopCpuCore();
    }
}
