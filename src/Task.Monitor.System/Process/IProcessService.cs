namespace Task.Monitor.System.Process;

public interface IProcessService
{
    ProcessInfo? GetProcessById(int pid);

    List<ProcessInfo> GetProcesses();
}
