namespace Task.Monitor.System.Process;

public sealed partial class ProcessService : IProcessService
{
    public List<ProcessInfo> GetProcesses() => GetProcessInfosInternal();

    public ProcessInfo? GetProcessById(int pid) => GetProcessInfoInternal(pid);
}
