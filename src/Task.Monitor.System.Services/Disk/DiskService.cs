namespace Task.Monitor.System.Services.Disk;

public sealed partial class DiskService : WorkerService
{
    private DiskSpecs diskSpecs = new();

    protected override void OnStart()
    {
        OnStartDiskSpecs(diskSpecs);
        OnStartDiskMetrics();
    }

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        // Caters for new drives inserted or ejected.
        if (ConsumeRefreshRequest()) {
            DiskSpecs refreshedSpecs = new();
            OnStartDiskSpecs(refreshedSpecs);
            diskSpecs = refreshedSpecs;
        }

        DiskInfo diskInfo = new();
        diskInfo.Specs = diskSpecs;

        OnDoWorkDiskMetrics(diskInfo);
        Publish(diskInfo);
    }

    protected override void OnStop()
    {
        OnStopDiskMetrics();
    }
}
