namespace Task.Monitor.System.Services.Disk;

public sealed partial class DiskService : WorkerService
{
    // How many samples in a row a disk may be missing before its state is dropped, so a disk absent
    // from a single sample (a slow or busy device) keeps its running totals.
    private const int MaxMissedSamples = 3;

    private DiskSpecs diskSpecs = new();

    // Per-disk sampling state, pruned once the disk stops appearing in samples.
    private interface IMissedSampleCounter
    {
        int ConsecutiveMisses { get; set; }
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

    protected override void OnStart()
    {
        OnStartDiskSpecs(diskSpecs);
        OnStartDiskMetrics();
    }

    protected override void OnStop()
    {
        OnStopDiskMetrics();
    }

    // Resets the miss count of every disk still being sampled, and drops a disk's state once it has
    // been missing for MaxMissedSamples samples in a row.
    private static void PruneMissing<TState>(Dictionary<string, TState> states, Func<string, bool> isLive)
        where TState : class, IMissedSampleCounter
    {
        List<string>? stale = null;

        foreach ((string name, TState state) in states) {
            if (isLive(name)) {
                state.ConsecutiveMisses = 0;
                continue;
            }

            if (++state.ConsecutiveMisses >= MaxMissedSamples) {
                (stale ??= []).Add(name);
            }
        }

        if (stale is null) {
            return;
        }

        foreach (string name in stale) {
            states.Remove(name);
        }
    }
}
