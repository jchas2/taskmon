namespace Task.Monitor.System.Services.Process;

public sealed partial class ProcessService : WorkerService
{
    private readonly ProcessSpecs processSpecs = new();

    public bool IrixMode { get; set; }

    protected override void OnStart()
    {
        OnStartProcessSpecs(processSpecs);
    }

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        ProcessInfo processInfo = new();
        processInfo.Specs = processSpecs;
        processSpecs.IrixMode = IrixMode;

        OnDoWorkProcessMetrics(processInfo.Metrics, processSpecs);
        Publish(processInfo);
    }

    protected override void OnStop() => OnStopProcessMetrics();
}
