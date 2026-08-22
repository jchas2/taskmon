namespace Task.Monitor.System.Process;

public interface IProcessService
{
    List<ProcessInfo> GetProcesses();
    ProcessInfo? GetProcessById(int pid);
}
