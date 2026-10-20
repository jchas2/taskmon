namespace Task.Monitor.System.Process;

public sealed partial class ProcessService : IProcessService
{
    public ProcessInfo? GetProcessById(int pid) => GetProcessInfoInternal(pid);

    public List<ProcessInfo> GetProcesses() => GetProcessInfosInternal();
}
