namespace Task.Monitor.System.Process;

public sealed partial class ProcessService : IProcessService
{
    public IEnumerable<ProcessInfo> GetProcesses()
    {
        foreach (ProcessInfo processInfo in GetProcessInfosInternal()) {
            yield return processInfo;
        }
    }

    public ProcessInfo? GetProcessById(int pid) => GetProcessInfoInternal(pid);
}
