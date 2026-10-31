using Task.Monitor.System.Process;

namespace Task.Monitor.Tests.Process;

public sealed class ProcessServiceFake : IProcessService
{
    private readonly List<ProcessInfo> processInfos = [];

    public void AddProcessInfo(ProcessInfo processInfo) =>
        processInfos.Add(processInfo);

    public ProcessInfo? GetProcessById(int pid)
    {
        RequestedPids.Add(pid);
        return processInfos.SingleOrDefault(p => p.Pid == pid);
    }

    public List<ProcessInfo> GetProcesses() => processInfos;

    public List<int> RequestedPids { get; } = [];
}