namespace Task.Monitor.System.Services.Startup;

public sealed partial class StartupService : WorkerService
{
    private const int RescanEveryCycles = 20;

    private StartupSpecs startupSpecs = new();
    private int cyclesSinceScan;

    protected override void OnStart() =>
        startupSpecs = ScanStartup();

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        if (ConsumeRefreshRequest() || ++cyclesSinceScan >= RescanEveryCycles) {
            startupSpecs = ScanStartup();
            cyclesSinceScan = 0;
        }

        Publish(new StartupInfo { Specs = startupSpecs });
    }

    private partial StartupSpecs ScanStartup();

#if !__WIN32__
    private partial StartupSpecs ScanStartup() => new();
#endif
}
