using System.Diagnostics;
using System.Runtime.CompilerServices;
using Task.Monitor.Cli.Utils;
using WorkerTask = System.Threading.Tasks.Task;

namespace Task.Monitor.System.Services;

public class WorkerService : ISystemService
{
    public const int DefaultDelayInMilliseconds = 1500;
    public const int MinimumDelayInMilliseconds = 500;

    internal const int ExitIntervalInMilliseconds = 250;

    private WorkerTask? workerTask;
    private CancellationTokenSource? cancellationTokenSource;

    private readonly ManualResetEventSlim wakeSignal = new(initialState: false);

    private volatile bool refreshRequested;
    private volatile int  delayInMilliseconds = DefaultDelayInMilliseconds;

    protected volatile ServiceStatus serviceStatus = ServiceStatus.None;

    protected bool ConsumeRefreshRequest()
    {
        if (!refreshRequested) {
            return false;
        }

        refreshRequested = false;
        return true;
    }

    internal ServiceController? Controller { get; set; }

    public int Delay
    {
        get => delayInMilliseconds;
        set => delayInMilliseconds = Math.Max(value, MinimumDelayInMilliseconds);
    }

    private void DoWork(CancellationToken cancellationToken)
    {
        long startTimestamp = Stopwatch.GetTimestamp();

        Trace.WriteLine($"{ServiceName}: starting");
        wakeSignal.Reset();
        OnStart();

        IsFirstCycle = true;

        while (!cancellationToken.IsCancellationRequested) {
            OnDoWork(cancellationToken);

            if (IsFirstCycle) {
                IsFirstCycle = false;

                Trace.WriteLine(
                    $"{ServiceName}: started in {Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds:0} ms, " +
                    $"status {serviceStatus}");
            }

            ThreadSleep(cancellationToken);
        }

        OnStop();
    }

    protected T? GetLatest<T>() where T : class =>
        Controller?.GetLatestInfo<T>();

    protected bool IsFirstCycle { get; private set; }

    protected virtual void OnDoWork(CancellationToken cancellationToken) { }

    protected virtual void OnStart() { }

    protected virtual void OnStop() { }

    protected void Publish<T>(T info) where T : class =>
        Controller?.Store(typeof(T), info);

    public void RequestImmediateRefresh()
    {
        refreshRequested = true;
        wakeSignal.Set();
    }

    protected string ServiceName => GetType().Name;

    public virtual void Start()
    {
        serviceStatus           = ServiceStatus.Starting;
        cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Token.Register(wakeSignal.Set);

        serviceStatus = ServiceStatus.Running;

        workerTask = WorkerTask.Run(() => {
            try {
                DoWork(cancellationTokenSource.Token);
            }
            catch (Exception ex) {
                serviceStatus = ServiceStatus.Errored;
                Trace.WriteLine($"{ServiceName}: FAILED with {ex.GetType().Name}, the service has stopped");
                ExceptionHelper.LogException(ex, ServiceName);
            }
        });
    }

    public ServiceStatus Status => serviceStatus;

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
        long startTicks = Stopwatch.GetTimestamp();

        while (!cancellationToken.IsCancellationRequested) {
            double elapsedMs = (Stopwatch.GetTimestamp() - startTicks) * 1000.0 / Stopwatch.Frequency;
            double remainingMs = Delay - elapsedMs;

            if (remainingMs <= 0.0) {
                return;
            }

            int waitMs = (int)Math.Min(ExitIntervalInMilliseconds, remainingMs);

             if (wakeSignal.Wait(waitMs)) {
                wakeSignal.Reset();
                return;
            }
        }
    }

    // Runs the next cycle now, without asking it to refresh.
    protected void Wake() => wakeSignal.Set();
}
