using System.Diagnostics.CodeAnalysis;
using Task.Monitor.Cli.Utils;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.System.Services.Process;

public partial class ProcessService : WorkerService
{
    private readonly ProcessSpecs processSpecs = new();

    public virtual bool EndTask(int pid, int timeoutMilliseconds)
    {
        if (!TryGetProcessByPid(pid, out SysDiag::Process? process)) {
            return false;
        }

        using (process) {
            try {
                process.Kill(entireProcessTree: true);
                return process.WaitForExit(timeoutMilliseconds);
            }
            catch (Exception ex) {
                ExceptionHelper.LogException(ex, $"Exception occurred terminating process {pid}.");
                return false;
            }
        }
    }

    public virtual List<ModuleInfo> GetModules(int pid)
    {
        if (!TryGetProcessByPid(pid, out SysDiag::Process? process)) {
            return [];
        }

        using (process) {
            GetModulesInternal(process, out List<ModuleInfo> moduleInfos);
            return moduleInfos;
        }
    }

    public virtual ProcessSample? GetProcessSample(int pid) => GetProcessSampleInternal(pid);

    public virtual List<ThreadInfo> GetThreads(int pid)
    {
        if (!TryGetProcessByPid(pid, out SysDiag::Process? process)) {
            return [];
        }

        using (process) {
            TryGetThreadsInternal(process, out List<ThreadInfo> threadInfos);
            return threadInfos;
        }
    }

    public bool IrixMode { get; set; }

    protected override void OnDoWork(CancellationToken cancellationToken)
    {
        ProcessInfo processInfo = new();
        processInfo.Specs       = processSpecs;

        processSpecs.IrixMode   = IrixMode;

        OnDoWorkProcessMetrics(processInfo.Metrics, processSpecs);
        Publish(processInfo);
    }

    protected override void OnStart()
    {
        OnStartProcessSpecs(processSpecs);
    }

    protected override void OnStop() => OnStopProcessMetrics();

    internal static bool TryGetProcessByPid(int pid, [NotNullWhen(true)] out SysDiag::Process? process)
    {
        if (pid < 0) {
            process = null;
            return false;
        }

        try {
            process = SysDiag::Process.GetProcessById(pid);
            return true;
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex, $"Failed GetProcessById() for Pid {pid}.");
            process = null;
            return false;
        }
    }

    private static bool TryGetThreadsInternal(SysDiag::Process process, out List<ThreadInfo> threadInfos)
    {
        threadInfos = new List<ThreadInfo>();

        // TODO: Native APIs.
        try {
            foreach (SysDiag::ProcessThread thread in process.Threads) {

                ThreadInfo threadInfo = new() {
                    ThreadId      = thread.Id,
                    ThreadState   = $"{thread.ThreadState}",
                    Reason        = thread.ThreadState == SysDiag.ThreadState.Wait ? $"{thread.WaitReason}" : string.Empty,
                    Priority      = thread.CurrentPriority,
                    StartAddress  = thread.StartAddress.ToInt64(),
                    CpuKernelTime = thread.PrivilegedProcessorTime,
                    CpuUserTime   = thread.UserProcessorTime,
                    CpuTotalTime  = thread.TotalProcessorTime
                };

                threadInfos.Add(threadInfo);
            }

            return true;
        }
        catch (Exception ex) {
            ExceptionHelper.LogException(ex);
            return false;
        }
    }
}
