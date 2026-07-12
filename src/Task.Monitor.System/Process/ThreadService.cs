using Task.Monitor.Cli.Utils;
using SysDiag = System.Diagnostics;

namespace Task.Monitor.System.Process;

public class ThreadService : IThreadService
{
    public virtual List<ThreadInfo>  GetThreads(int pid)
    {
        if (!ProcessUtils.TryGetProcessByPid(pid, out SysDiag::Process? process) ||
            process == null) {
            return [];
        }

        TryGetThreadsInternal(process, out var threadInfos);
        process.Dispose();
        return threadInfos;
    }

    private bool TryGetThreadsInternal(SysDiag::Process process, out List<ThreadInfo> threadInfos)
    {
        threadInfos = new List<ThreadInfo>();

        // TODO: Native APIs.
        try {
            foreach (SysDiag::ProcessThread thread in process.Threads) {

                ThreadInfo threadInfo = new() {
                    ThreadId = thread.Id,
                    ThreadState = $"{thread.ThreadState}",
                    Reason = thread.ThreadState == SysDiag.ThreadState.Wait
                        ? $"{thread.WaitReason}"
                        : string.Empty,
                    Priority = thread.CurrentPriority,
                    StartAddress = thread.StartAddress.ToInt64(),
                    CpuKernelTime = thread.PrivilegedProcessorTime,
                    CpuUserTime = thread.UserProcessorTime,
                    CpuTotalTime = thread.TotalProcessorTime
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