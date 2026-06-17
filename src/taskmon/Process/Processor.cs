using System.Runtime.CompilerServices;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System;
using Task.Monitor.System.Process;
using WorkerTask = System.Threading.Tasks.Task;

namespace Task.Monitor.Process;

public class Processor : IProcessor
{
    internal const int DefaultDelayInMilliseconds = 1500;
    internal const int MinimumDelayInMilliseconds = 500;
    internal const int ExitIntervalInMilliseconds = 250;
    internal const int DefaultIterationLimit = 0;

    private const int InitialCapacity = 2048;

    private readonly IProcessService processService;
    private SystemStatistics systemStatistics;
    private readonly List<ProcessorInfo> allProcessorInfos;
    private readonly List<ProcessorInfo> allProcessorInfosCopy;
    private readonly Dictionary<int, ProcessInfo> processInfoMap;
    private readonly Dictionary<int, ProcessInfo> newProcessInfoMap;
    private readonly Dictionary<int, ProcessAverage> averageState;
    private readonly HashSet<int> currentPids;
    private readonly List<int> stalePids;
    private WorkerTask? workerTask;
    private WorkerTask? monitorTask;
    private CancellationTokenSource? cancellationTokenSource;
    private readonly Lock @lock;
    private readonly bool isWindows = false;
    private volatile bool dataInitialised = false;
    private int processCount = 0;
    private int threadCount = 0;
    private int delayInMilliseconds = DefaultDelayInMilliseconds;
    private int iterationLimit = DefaultIterationLimit;
    
    public event EventHandler<ProcessorEventArgs>? ProcessorUpdated;
    
    public Processor(IProcessService processService)
    {
        this.processService = processService;
        Delay = DefaultDelayInMilliseconds;
        systemStatistics = new SystemStatistics();
        allProcessorInfos = new List<ProcessorInfo>(InitialCapacity);
        allProcessorInfosCopy = new List<ProcessorInfo>(InitialCapacity);
        processInfoMap = new Dictionary<int, ProcessInfo>(InitialCapacity);
        newProcessInfoMap = new Dictionary<int, ProcessInfo>(InitialCapacity);
        averageState = new Dictionary<int, ProcessAverage>(InitialCapacity);
        currentPids = new HashSet<int>(InitialCapacity);
        stalePids = new List<int>(InitialCapacity);
        @lock = new Lock();
        isWindows = OperatingSystem.IsWindows();
    }

    public int Delay
    {
        get => delayInMilliseconds;
        set => delayInMilliseconds = value >= MinimumDelayInMilliseconds 
            ? value 
            : DefaultDelayInMilliseconds;
    }

    private void GetNetworkStats(ref NetworkStatistics networkStatistics)
    {
        if (!SystemInfo.GetNetworkStats(ref networkStatistics)) {
            networkStatistics.NetworkBytesReceived = 0;
            networkStatistics.NetworkBytesSent = 0;
            networkStatistics.NetworkPacketsReceived = 0;
            networkStatistics.NetworkPacketsSent = 0;
        }
    }
    
    private void GetSystemTimes(ref SystemTimes systemTimes)
    {
        if (!SystemInfo.GetCpuTimes(ref systemTimes)) {
            systemTimes.Idle = 0;
            systemTimes.Kernel = 0;
            systemTimes.User = 0;
        }
    }

#if __WIN32__
    public bool IrixMode { get; set; } = false;
#endif
#if __APPLE__
    public bool IrixMode { get; set; } = true;
#endif

    public bool IsRunning => workerTask?.IsCompleted == false && monitorTask?.IsCompleted == false;
    
    public int IterationLimit
    {
        get => iterationLimit;
        set => iterationLimit = value >= DefaultIterationLimit 
            ? value 
            : DefaultIterationLimit;
    }

    public int ProcessCount => processCount;

    public void Run()
    {
        cancellationTokenSource = new CancellationTokenSource();
       
        workerTask = WorkerTask.Run(() => RunInternal(cancellationTokenSource.Token));
        monitorTask = WorkerTask.Run(() => RunMonitorInternal(cancellationTokenSource.Token));
    }

#if __APPLE__    
    // Required for MacOS Arm64, otherwise aggressively optimizes and pegs the CPU.                                                                                  
    [MethodImpl(MethodImplOptions.NoOptimization)]
#endif
    private void RunInternal(CancellationToken cancellationToken)
    {
        SystemTimes prevSysTimes = new();
        SystemTimes currSysTimes = new();
        SystemTimes sysTimesDeltas = new();
        NetworkStatistics prevNetworkStats = new();
        NetworkStatistics currNetworkStats = new();
        TimeSpan delayInMs = new TimeSpan(0, 0, 0, 0, Delay);
        int iterationCount = 0;
        int irixFactor = 0;
        
        SystemInfo.GetSystemInfo(ref systemStatistics);

        while (!cancellationToken.IsCancellationRequested && iterationCount <= IterationLimit) {
            allProcessorInfos.Clear();
            processInfoMap.Clear();
            newProcessInfoMap.Clear();
            currentPids.Clear();
            processCount = 0;
            threadCount = 0;

            GetSystemTimes(ref prevSysTimes);
            GetNetworkStats(ref prevNetworkStats);

            foreach (ProcessInfo processInfo in processService.GetProcesses()) {
#if __WIN32__
                // On Windows, ignore the system "idle" process auto assigned to Pid 0.
                if (isWindows && processInfo.Pid == 0) {
                    continue;
                }
#endif
                processInfoMap.TryAdd(processInfo.Pid, processInfo);
                processCount++;
                threadCount += processInfo.ThreadCount; 
            }
            
            if (cancellationToken.IsCancellationRequested) {
                return;
            }

            ThreadSleep(cancellationToken, Delay);
            
            if (cancellationToken.IsCancellationRequested) {
                return;
            }            
            
            GetSystemTimes(ref currSysTimes);
            GetNetworkStats(ref currNetworkStats);

            sysTimesDeltas.Idle = currSysTimes.Idle - prevSysTimes.Idle;
            sysTimesDeltas.Kernel = currSysTimes.Kernel - prevSysTimes.Kernel;
            sysTimesDeltas.User = currSysTimes.User - prevSysTimes.User;
            
            systemStatistics.CpuPercentIdleTime = 0.0;
            systemStatistics.CpuPercentUserTime = 0.0;
            systemStatistics.CpuPercentKernelTime = 0.0;
            systemStatistics.GpuPercentTime = 0.0;
            systemStatistics.TotalDiskReadBytes = 0;
            systemStatistics.TotalDiskWriteBytes = 0;
            systemStatistics.DiskUsage = 0;

            SystemInfo.GetSystemMemory(ref systemStatistics);
            SystemInfo.GetGpuMemory(ref systemStatistics);
#if __APPLE__
            // Device-wide GPU utilisation from the driver (captures media/codec/compositor
            // work that per-process accumulatedGPUTime misses). Sets GpuPercentTime directly;
            // the per-process summation below is Windows-only.
            SystemInfo.GetGpuUsage(ref systemStatistics);
#endif
#if __WIN32__
            long totalSysTime = Environment.ProcessorCount * delayInMs.Ticks;
#endif              
#if __APPLE__
            long totalSysTime = sysTimesDeltas.Kernel + sysTimesDeltas.User + sysTimesDeltas.Idle;
#endif
            foreach (ProcessInfo processInfo in processService.GetProcesses()) {
                newProcessInfoMap.TryAdd(processInfo.Pid, processInfo);
            }

            foreach (int pid in processInfoMap.Keys) {
                ProcessInfo processInfo = processInfoMap[pid];
                ProcessInfo? newProcessInfo = newProcessInfoMap.GetValueOrDefault(pid);

                if (newProcessInfo == null) {
                    continue;
                }

                ProcessorInfo processorInfo = new() {
                    Pid = processInfo.Pid,
                    ParentPid = processInfo.ParentPid,
                    IsDaemon = processInfo.IsDaemon,
                    IsLowPriority = processInfo.IsLowPriority,
                    ProcessName = processInfo.ProcessName,
                    FileDescription = processInfo.FileDescription,
                    UserName = processInfo.UserName,
                    CmdLine = processInfo.CmdLine,
                    ThreadCount = processInfo.ThreadCount,
                    HandleCount = processInfo.HandleCount,
                    BasePriority = processInfo.BasePriority
                };
                
                long procKernelDiff = newProcessInfo.KernelTime - processInfo.KernelTime;
                long procUserDiff = newProcessInfo.UserTime - processInfo.UserTime;
                long totalProc = procKernelDiff + procUserDiff;

                if (totalSysTime == 0) {
                    continue;
                }

                // Irix mode is consistent with Mac Activity Monitor, where 100% is full utilisation of a SINGLE cpu core on the system.
                // Non-Irix Mode is consistent with Windows Task.Monitor where 100% is full utilisation of ALL cores on the system.
                irixFactor = IrixMode ? Environment.ProcessorCount : 1;
#if __WIN32__
                processorInfo.CpuTimePercent = irixFactor * (double)totalProc / (double)totalSysTime;
                processorInfo.CpuKernelTimePercent = irixFactor * (double)procKernelDiff / (double)totalSysTime;
                processorInfo.CpuUserTimePercent = irixFactor * (double)procUserDiff / (double)totalSysTime;
#endif
#if __APPLE__
                processorInfo.CpuTimePercent = irixFactor * (double)totalProc / (double)(delayInMs.Ticks * (long)Environment.ProcessorCount);
                processorInfo.CpuKernelTimePercent = irixFactor * (double)procKernelDiff / (double)(delayInMs.Ticks * (long)Environment.ProcessorCount);
                processorInfo.CpuUserTimePercent = irixFactor * (double)procUserDiff / (double)(delayInMs.Ticks * (long)Environment.ProcessorCount);
#endif
                long gpuTimeDelta = newProcessInfo.GpuTime - processInfo.GpuTime;
                double gpuPercent = 0.0;
                
                if (gpuTimeDelta > 0) {
#if __WIN32__
                    double deltaSeconds = gpuTimeDelta / 10_000_000.0;
                    gpuPercent = deltaSeconds / ((double)Delay / 1000);
                    processorInfo.GpuTimePercent = gpuPercent;
#endif
#if __APPLE__
                    // Convert nanoseconds to percentage of delay time.
                    double deltaSeconds = gpuTimeDelta / 1_000_000_000.0;
                    gpuPercent = deltaSeconds / ((double)Delay / 1000);
                    processorInfo.GpuTimePercent = gpuPercent;
#endif
#if __WIN32__
                    // macOS sets the system total from the device-wide gauge in GetGpuUsage.
                    systemStatistics.GpuPercentTime += gpuPercent;
#endif
                }

                processorInfo.UsedMemory = newProcessInfo.UsedMemory;
                processorInfo.DiskReadBytes = newProcessInfo.DiskReadBytes;
                processorInfo.DiskWriteBytes = newProcessInfo.DiskWriteBytes;
                
                processorInfo.DiskUsage = 
                    (long)((double)(newProcessInfo.DiskOperations - processInfo.DiskOperations) * (1000.0 / (double)Delay));

                systemStatistics.TotalDiskReadBytes += (long)processorInfo.DiskReadBytes;
                systemStatistics.TotalDiskWriteBytes += (long)processorInfo.DiskWriteBytes;
                systemStatistics.DiskUsage += processorInfo.DiskUsage;

                if (!averageState.TryGetValue(pid, out ProcessAverage? average)) {
                    average = new ProcessAverage();
                    averageState.Add(pid, average);
                }

                average.Add(processorInfo);

                processorInfo.CpuTimePercentAvg = average.CpuTimePercent;
                processorInfo.GpuTimePercentAvg = average.GpuTimePercent;
                processorInfo.UsedMemoryAvg = average.UsedMemory;
                processorInfo.DiskUsageAvg = average.DiskUsage;

                currentPids.Add(pid);

                allProcessorInfos.Add(processorInfo);
            }

            if (averageState.Count > currentPids.Count) {
                stalePids.Clear();

                foreach (int pid in averageState.Keys) {
                    if (!currentPids.Contains(pid)) {
                        stalePids.Add(pid);
                    }
                }

                for (int i = 0; i < stalePids.Count; i++) {
                    averageState.Remove(stalePids[i]);
                }
            }

            systemStatistics.CpuPercentUserTime = (double)sysTimesDeltas.User / (double)totalSysTime;
            systemStatistics.CpuPercentKernelTime = (double)sysTimesDeltas.Kernel / (double)totalSysTime;
            systemStatistics.CpuPercentIdleTime = (double)sysTimesDeltas.Idle / (double)totalSysTime;
            systemStatistics.ProcessCount = processCount;
            systemStatistics.ThreadCount = threadCount;
            
            systemStatistics.TotalNetworkBytesReceived = currNetworkStats.NetworkBytesReceived;
            systemStatistics.TotalNetworkBytesSent = currNetworkStats.NetworkBytesSent;
            systemStatistics.TotalNetworkPacketsReceived = currNetworkStats.NetworkPacketsReceived;
            systemStatistics.TotalNetworkPacketsSent = currNetworkStats.NetworkPacketsSent;
            
            systemStatistics.NetworkBytesReceiveTime =
                (ulong)((double)(currNetworkStats.NetworkBytesReceived - prevNetworkStats.NetworkBytesReceived) * (1000.0 / (double)Delay)); 
            systemStatistics.NetworkBytesSendTime =
                (ulong)((double)(currNetworkStats.NetworkBytesSent - prevNetworkStats.NetworkBytesSent) * (1000.0 / (double)Delay)); 
            systemStatistics.NetworkPacketsReceiveTime =
                (ulong)((double)(currNetworkStats.NetworkPacketsReceived - prevNetworkStats.NetworkPacketsReceived) * (1000.0 / (double)Delay)); 
            systemStatistics.NetworkPacketsSendTime =
                (ulong)((double)(currNetworkStats.NetworkPacketsSent - prevNetworkStats.NetworkPacketsSent) * (1000.0 / (double)Delay)); 
            
            lock (@lock) {
                allProcessorInfosCopy.Clear();

                for (int i = 0; i < allProcessorInfos.Count; i++) {
                    allProcessorInfosCopy.Add(allProcessorInfos[i]);
                }
            }

            if (!dataInitialised) {
                dataInitialised = true;
            }

            if (iterationLimit > 0) {
                iterationCount++;
            }
        }
    }

#if __APPLE__    
    // Required for MacOS Arm64, otherwise aggressively optimizes and pegs the CPU.                                                                                  
    [MethodImpl(MethodImplOptions.NoOptimization)]
#endif
    private void RunMonitorInternal(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested) {
            ThreadSleep(cancellationToken, Delay);

            if (cancellationToken.IsCancellationRequested) {
                return;
            }            
            
            if (!dataInitialised) {
                continue;
            }
            
            lock (@lock) {
                ProcessorEventArgs eventArgs = new(allProcessorInfosCopy, systemStatistics);
                ProcessorUpdated?.Invoke(this, eventArgs);
            }
        }
    }

    public void Stop()
    {
        cancellationTokenSource?.Cancel();
        WorkerTask[] workerTasks = [workerTask!, monitorTask!];
        
        try {
            WorkerTask.WaitAll(workerTasks);
        }
        catch (AggregateException aggEx) {
            ExceptionHelper.HandleWaitAllException(aggEx);
        }
    }

#if __APPLE__    
    // Required for MacOS Arm64, otherwise aggressively optimizes and pegs the CPU.                                                                                  
    [MethodImpl(MethodImplOptions.NoOptimization)]
#endif
    private void ThreadSleep(CancellationToken cancellationToken, int delay)
    {
        // Method to only sleep the thread the minimum amount of required time
        // with checks every ExitIntervalInMilliseconds. For example, if the delay is 
        // 3000ms, check every 250ms for a cancellation. 
        // This keeps the response to the ESC key interrupt snappy.
        TimeSpan remainingDelay = new TimeSpan(0, 0, 0, 0, delay);
        
        while (remainingDelay > TimeSpan.Zero) {
            if (cancellationToken.IsCancellationRequested) {
                return;
            }
        
            Thread.Sleep((int)Math.Min(ExitIntervalInMilliseconds, remainingDelay.TotalMilliseconds));
            remainingDelay = remainingDelay.Subtract(TimeSpan.FromMilliseconds(ExitIntervalInMilliseconds));
        }
    }

    public int ThreadCount => threadCount;
}
