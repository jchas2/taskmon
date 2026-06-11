using Task.Monitor.System.Process;

namespace Task.Monitor.Tests.Process;

public sealed class ProcessServiceFake : IProcessService
{
    private readonly List<ProcessInfo> processInfos = [];

    public void AddProcessInfo(ProcessInfo processInfo) =>
        processInfos.Add(processInfo);

    public IEnumerable<ProcessInfo> GetProcesses() => processInfos;

    public ProcessInfo? GetProcessById(int pid) =>
        processInfos.SingleOrDefault(p => p.Pid == pid);
}