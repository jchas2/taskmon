using Task.Monitor.System.Process;

namespace Task.Monitor.Tests.Process;

public sealed class ThreadServiceFake : IThreadService
{
    private readonly List<ThreadInfo> threadInfos = [];

    public ThreadServiceFake Add(ThreadInfo threadiInfo)
    {
        threadInfos.Add(threadiInfo);
        return this;
    }

    public List<ThreadInfo> GetThreads(int pid)
    {
        RequestedPids.Add(pid);
        return threadInfos;
    }

    public List<int> RequestedPids { get; } = [];
}
