using System.Diagnostics;
using Task.Monitor.Cli.Utils;
using WorkerTask = System.Threading.Tasks.Task;

namespace Task.Monitor.System.Services;

public class WorkerService : ISystemService
{
    // Public because Delay is: a caller that validates a value before assigning it needs the same
    // bounds the setter applies, and the config layer needs the default to seed a new config file.
    public const int DefaultDelayInMilliseconds = 1500;
    public const int MinimumDelayInMilliseconds = 500;

    internal const int ExitIntervalInMilliseconds = 250;

    private WorkerTask? workerTask;
    private CancellationTokenSource? cancellationTokenSource;

    // Set to end the current wait early: a device notification or a cancellation, so the worker
    // runs its next cycle now instead of sleeping out the rest of the delay.
    private readonly ManualResetEventSlim wakeSignal = new(initialState: false);

    // Read once at the top of a cycle by services that need to tell "the hardware changed" from
    // "time for another sample". Written from any thread, including a native notification callback.
    private volatile bool refreshRequested;

    // Written from the ui thread and read from this service's worker thread. An int cannot tear,
    // but without the barrier the worker can keep spinning on a stale value after a change.
    private volatile int delayInMilliseconds = DefaultDelayInMilliseconds;

    // Written from this service's worker thread and from Start/Stop; read from the controller's
    // worker thread when it builds a snapshot's service-health list. volatile keeps that reader from
    // spinning on a stale value after a transition.
    protected volatile ServiceStatus serviceStatus = ServiceStatus.None;

    internal ServiceController? Controller { get; set; }

    public int Delay
    {
        get => delayInMilliseconds;

        // Clamped up to the minimum rather than substituted with the default. Falling back to the
        // default would answer a request for a faster interval with a slower one than the caller
        // would have got by not asking at all.
        set => delayInMilliseconds = Math.Max(value, MinimumDelayInMilliseconds);
    }

    private void DoWork(CancellationToken cancellationToken)
    {
        // A previous run may have left the signal set (its cancellation), and the first cycle
        // runs before the first wait anyway.
        wakeSignal.Reset();

        OnStart();

        while (!cancellationToken.IsCancellationRequested) {
            OnDoWork(cancellationToken);
            ThreadSleep(cancellationToken);
        }

        OnStop();
    }

    protected virtual void OnDoWork(CancellationToken cancellationToken) { }

    // Asks the worker to run a cycle now rather than waiting out the rest of its delay, and to
    // re-enumerate hardware rather than just take another sample. Safe to call from any thread,
    // including a device change notification callback: it only sets a flag and an event.
    public void RequestImmediateRefresh()
    {
        refreshRequested = true;
        wakeSignal.Set();
    }

    // True once per hardware change: services call this at the top of a cycle to decide whether
    // to rebuild their device list before sampling.
    protected bool ConsumeRefreshRequest()
    {
        if (!refreshRequested) {
            return false;
        }

        refreshRequested = false;
        return true;
    }

    protected void Publish<T>(T info) where T : class =>
        Controller?.Store(typeof(T), info);

    // Reads another service's most recently published Info through the controller. Services stay
    // decoupled from each other's implementations: this is a read of a published data contract,
    // not a call into the service that produced it.
    protected T? GetLatest<T>() where T : class =>
        Controller?.GetLatestInfo<T>();

    protected virtual void OnStart() { }

    protected virtual void OnStop() { }
    
    public ServiceStatus Status => serviceStatus;
    
    public virtual void Start()
    {
        serviceStatus = ServiceStatus.Starting;
        cancellationTokenSource = new CancellationTokenSource();

        // A cancellation ends the current wait immediately instead of at the next 250ms step.
        cancellationTokenSource.Token.Register(wakeSignal.Set);

        workerTask = WorkerTask.Run(() => {
            try {
                DoWork(cancellationTokenSource.Token);
            }
            catch (Exception ex) {
                serviceStatus = ServiceStatus.Errored;
                ExceptionHelper.LogException(ex);
            }
        });
        serviceStatus = ServiceStatus.Running;
    }

    public virtual void Stop()
    {
        try {
            serviceStatus = ServiceStatus.Stopping;
            cancellationTokenSource?.Cancel();
            workerTask?.Wait();
            serviceStatus = ServiceStatus.Stopped;
        }
        catch (AggregateException aggEx) {
            ExceptionHelper.HandleWaitAllException(aggEx);
            serviceStatus = ServiceStatus.Errored;
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex);
            serviceStatus = ServiceStatus.Errored;
        }
    }
    
#if __APPLE__    
    // Required for macOS Arm64, otherwise aggressively optimizes and pegs the CPU.                                                                                  
    [MethodImpl(MethodImplOptions.NoOptimization)]
#endif
    private void ThreadSleep(CancellationToken cancellationToken)
    {
        // Sleeps in ExitIntervalInMilliseconds steps so a cancellation is noticed promptly rather
        // than at the end of a long delay.
        //
        // Delay is re-read on every step and compared against the interval that has actually
        // elapsed, rather than counting down a value captured on entry. That is what lets a delay
        // changed from setup take effect within one step: shortening it ends the wait already in
        // progress, lengthening it extends that wait rather than applying only to the next one.
        long startTicks = Stopwatch.GetTimestamp();

        while (!cancellationToken.IsCancellationRequested) {
            double elapsedMs = (Stopwatch.GetTimestamp() - startTicks) * 1000.0 / Stopwatch.Frequency;
            double remainingMs = Delay - elapsedMs;

            if (remainingMs <= 0.0) {
                return;
            }

            int waitMs = (int)Math.Min(ExitIntervalInMilliseconds, remainingMs);

            // A device change (RequestImmediateRefresh) or a cancellation ends the wait now; a
            // plain timeout falls through to re-check the elapsed time and the token.
            if (wakeSignal.Wait(waitMs)) {
                wakeSignal.Reset();
                return;
            }
        }
    }
}