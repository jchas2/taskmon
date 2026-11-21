using System.Diagnostics;
using Task.Monitor.Cli.Utils;
using WorkerTask = System.Threading.Tasks.Task;

namespace Task.Monitor.System.Services;

public class WorkerService : ISystemService
{
    public const int DefaultDelayInMilliseconds = 1500;
    public const int MinimumDelayInMilliseconds = 500;

    private WorkerTask? workerTask;
    private CancellationTokenSource? cancellationTokenSource;

    private readonly ManualResetEventSlim wakeSignal = new(initialState: false);

    private volatile bool          refreshRequested;
    private volatile int           delayInMilliseconds = DefaultDelayInMilliseconds;
    private volatile ServiceStatus status              = ServiceStatus.None;

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
        set {
            int previous = delayInMilliseconds;
            delayInMilliseconds = Math.Max(value, MinimumDelayInMilliseconds);

            if (delayInMilliseconds < previous) {
                wakeSignal.Set();
            }
        }
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
                    $"status {status}");
            }

            if (wakeSignal.Wait(Delay)) {
                wakeSignal.Reset();
            }
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

    protected bool RescanDue(ref int cyclesSinceScan, int everyCycles)
    {
        if (!ConsumeRefreshRequest() && ++cyclesSinceScan < everyCycles) {
            return false;
        }

        cyclesSinceScan = 0;
        return true;
    }

    protected string ServiceName => GetType().Name;

    public void Start()
    {
        status                  = ServiceStatus.Starting;
        cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Token.Register(wakeSignal.Set);

        status = ServiceStatus.Running;

        workerTask = WorkerTask.Run(() => {
            try {
                DoWork(cancellationTokenSource.Token);
            }
            catch (Exception ex) {
                status = ServiceStatus.Errored;
                Trace.WriteLine($"{ServiceName}: FAILED with {ex.GetType().Name}, the service has stopped");
                ExceptionHelper.LogException(ex, ServiceName);
            }
        });
    }

    public ServiceStatus Status
    {
        get           => status;
        protected set => status = value;
    }

    public void Stop()
    {
        try {
            status = ServiceStatus.Stopping;
            cancellationTokenSource?.Cancel();
            workerTask?.Wait();
            status = ServiceStatus.Stopped;
        }
        catch (AggregateException aggEx) {
            ExceptionHelper.HandleWaitAllException(aggEx);
            status = ServiceStatus.Errored;
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex);
            status = ServiceStatus.Errored;
        }
    }

    protected void Wake() => wakeSignal.Set();
}
