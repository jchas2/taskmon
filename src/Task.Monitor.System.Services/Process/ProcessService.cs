namespace Task.Monitor.System.Services.Process;

public sealed partial class ProcessService : WorkerService
{
    private readonly ProcessSpecs processSpecs = new();

    // Set from AppConfig at registration, the way Delay is.
    public bool IrixMode { get; set; }

    protected override void OnStart()
    {
        OnStartProcessSpecs(processSpecs);
    }

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        ProcessInfo processInfo = new();
        processInfo.Specs = processSpecs;

        // Re-read every cycle rather than only at start. Irix mode is the one spec here that is a
        // live setting: the ui toggles it with the I key, and the percentages published below have
        // to be computed with whatever it is now, not with whatever it was at startup.
        processSpecs.IrixMode = IrixMode;

        OnDoWorkProcessMetrics(processInfo.Metrics, processSpecs);
        Publish(processInfo);
    }

    protected override void OnStop()
    {
        OnStopProcessMetrics();
    }
}
