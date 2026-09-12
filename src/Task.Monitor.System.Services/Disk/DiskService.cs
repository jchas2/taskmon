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
        // A drive was inserted or ejected: rebuild the drive list so a new disk gets its model and
        // capacity, and a removed one drops out. OnStartDiskSpecs appends, so it runs against a
        // fresh DiskSpecs rather than the live one.
        if (ConsumeRefreshRequest()) {
            DiskSpecs refreshed = new();
            OnStartDiskSpecs(refreshed);
            diskSpecs = refreshed;
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
