using System.Diagnostics.CodeAnalysis;
using Task.Monitor.Cli.Utils;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.System.Services.Process;

public interface IProcessOperations
{
    public bool EndTask(int pid, int timeoutMilliseconds);

    public List<ModuleInfo> GetModules(int pid);

    public ProcessSample? GetProcessSample(int pid);

    public List<ThreadInfo> GetThreads(int pid);
}

internal sealed class ProcessOperations : IProcessOperations
{
    public bool EndTask(int pid, int timeoutMilliseconds)
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

    public List<ModuleInfo> GetModules(int pid)
    {
        if (!TryGetProcessByPid(pid, out SysDiag::Process? process)) {
            return [];
        }

        using (process) {
            ProcessService.GetModulesInternal(process, out List<ModuleInfo> moduleInfos);
            return moduleInfos;
        }
    }

    public ProcessSample? GetProcessSample(int pid) => ProcessService.GetProcessSampleInternal(pid);

    public List<ThreadInfo> GetThreads(int pid)
    {
        if (!TryGetProcessByPid(pid, out SysDiag::Process? process)) {
            return [];
        }

        List<ThreadInfo> threadInfos = [];

        using (process) {
            // TODO: Native APIs.
            try {
                foreach (SysDiag::ProcessThread thread in process.Threads) {
                    threadInfos.Add(new ThreadInfo {
                        ThreadId      = thread.Id,
                        ThreadState   = $"{thread.ThreadState}",
                        Reason        = thread.ThreadState == SysDiag.ThreadState.Wait ? $"{thread.WaitReason}" : string.Empty,
                        Priority      = thread.CurrentPriority,
                        StartAddress  = thread.StartAddress.ToInt64(),
                        CpuKernelTime = thread.PrivilegedProcessorTime,
                        CpuUserTime   = thread.UserProcessorTime,
                        CpuTotalTime  = thread.TotalProcessorTime
                    });
                }
            }
            catch (Exception ex) {
                ExceptionHelper.LogException(ex);
            }
        }

        return threadInfos;
    }

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
}
