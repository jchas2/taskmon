using System.Diagnostics;
using Task.Monitor.Cli.Utils;
using WorkerTask = System.Threading.Tasks.Task;

namespace Task.Monitor.System.Services.DiskSpace;

// Publishes the state of an on-demand folder-size scan, started and cancelled by the UI rather
// than run on a timer like every other service here - a full scan is too expensive to repeat on
// a fixed interval. The service itself is always Running from app start, matching every other
// service; DiskSpaceScanState (carried on the published Specs) is the separate, UI-driven notion
// of whether a scan is currently in progress.
//
// A scan runs on its own dedicated Task rather than inside OnDoWork's shared tick loop, because it
// needs to make progress in real wall-clock time rather than one small step per shared sampling
// interval. It publishes its own throttled progress directly from that thread - Publish() is just
// a ConcurrentDictionary write, so this needs no extra synchronisation with OnDoWork's tick.
public sealed class DiskSpaceService : WorkerService
{
    private static readonly TimeSpan PublishThrottle = TimeSpan.FromMilliseconds(200);

    private readonly object startLock = new();

    private volatile DiskSpaceSpecs specs = new();
    private CancellationTokenSource? scanCts;
    private WorkerTask? scanTask;

    protected override void OnDoWork(CancellationToken cancellationToken) =>
        Publish(new DiskSpaceInfo { Specs = specs });

    protected override void OnStop() => CancelScan();

    public void StartScan(string rootPath)
    {
        lock (startLock) {
            if (specs.State == DiskSpaceScanState.Scanning) {
                return;
            }

            DiskSpaceAccumulator accumulator = new(rootPath);
            scanCts = new CancellationTokenSource();
            CancellationToken token = scanCts.Token;

            UpdateSpecs(accumulator.Snapshot(DiskSpaceScanState.Scanning, elapsedMilliseconds: 0));
            scanTask = WorkerTask.Run(() => RunScan(rootPath, accumulator, token));
        }
    }

    // Safe to call whether or not a scan is running - CancellationTokenSource.Cancel() on a
    // finished or null source is either a no-op or nothing to do.
    public void CancelScan() => scanCts?.Cancel();

    private void RunScan(string rootPath, DiskSpaceAccumulator accumulator, CancellationToken cancellationToken)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        Stopwatch throttle = Stopwatch.StartNew();

        try {
            DiskSpaceWalker.Walk(rootPath, accumulator, cancellationToken, onFolderVisited: () => {
                if (throttle.Elapsed < PublishThrottle) {
                    return;
                }

                UpdateSpecs(accumulator.Snapshot(DiskSpaceScanState.Scanning, elapsed.ElapsedMilliseconds));
                throttle.Restart();
            });

            accumulator.MarkScanComplete();
            UpdateSpecs(accumulator.Snapshot(DiskSpaceScanState.Completed, elapsed.ElapsedMilliseconds));
        }
        catch (OperationCanceledException) {
            UpdateSpecs(accumulator.Snapshot(DiskSpaceScanState.Cancelled, elapsed.ElapsedMilliseconds));
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex);
            UpdateSpecs(accumulator.Snapshot(DiskSpaceScanState.Faulted, elapsed.ElapsedMilliseconds, ex.Message));
        }
    }

    private void UpdateSpecs(DiskSpaceSpecs newSpecs)
    {
        specs = newSpecs;
        Publish(new DiskSpaceInfo { Specs = newSpecs });
    }
}
