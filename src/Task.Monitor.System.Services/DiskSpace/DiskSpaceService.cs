using System.Diagnostics;
using Task.Monitor.Cli.Utils;
using WorkerTask = System.Threading.Tasks.Task;

namespace Task.Monitor.System.Services.DiskSpace;

public sealed class DiskSpaceService : WorkerService
{
    private static readonly TimeSpan PublishThrottle = TimeSpan.FromMilliseconds(200);

    private readonly object startLock = new();

    private volatile DiskSpaceSpecs specs = new();
    private CancellationTokenSource? scanCts;
    private WorkerTask? scanTask;

    protected override void OnStart() => Publish(new DiskSpaceInfo { Specs = specs });

    protected override void OnDoWork(CancellationToken cancellationToken) { }

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

    public void CancelScan() => scanCts?.Cancel();

    private void RunScan(string rootPath, DiskSpaceAccumulator accumulator, CancellationToken cancellationToken)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        Stopwatch throttle = Stopwatch.StartNew();

        try {
            DiskSpaceWalker.Walk(
                rootPath, 
                accumulator, 
                cancellationToken, 
                onFolderVisited: () => {
                    
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
