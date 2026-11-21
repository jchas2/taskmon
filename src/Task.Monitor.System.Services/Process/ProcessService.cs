namespace Task.Monitor.System.Services.Process;

public sealed partial class ProcessService : WorkerService
{
    private readonly ProcessSpecs processSpecs = new();

    public ProcessService(IProcessOperations? operations = null) =>
        Operations = operations ?? new ProcessOperations(this);

    public bool IrixMode { get; set; }

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        processSpecs.IrixMode = IrixMode;

        ProcessInfo processInfo = new() { Specs = processSpecs };

        OnDoWorkProcessMetrics(processInfo.Metrics, processSpecs);
        Publish(processInfo);
    }

    protected override void OnStop() => OnStopProcessMetrics();

    public IProcessOperations Operations { get; }
}
