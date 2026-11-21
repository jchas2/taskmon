using Task.Monitor.System.Services.Process;

namespace Task.Monitor.Tests.Process;

// Stands in for ProcessService's single-process calls, passed to a ProcessService registered in the
// ServiceController. It reads nothing from the system and ends no real processes.
public sealed class ProcessOperationsFake : IProcessOperations
{
    private readonly List<ModuleInfo>    moduleInfos = [];
    private readonly List<ProcessSample> samples     = [];
    private readonly List<ThreadInfo>    threadInfos = [];

    public ProcessOperationsFake Add(ModuleInfo moduleInfo)
    {
        moduleInfos.Add(moduleInfo);
        return this;
    }

    public ProcessOperationsFake Add(ProcessSample sample)
    {
        samples.Add(sample);
        return this;
    }

    public ProcessOperationsFake Add(ThreadInfo threadInfo)
    {
        threadInfos.Add(threadInfo);
        return this;
    }

    public List<int> EndedPids { get; } = [];

    public bool EndTask(int pid, int timeoutMilliseconds)
    {
        EndedPids.Add(pid);
        return true;
    }

    public List<ModuleInfo> GetModules(int pid)
    {
        ModuleRequestedPids.Add(pid);
        return moduleInfos;
    }

    public ProcessSample? GetProcessSample(int pid)
    {
        SampleRequestedPids.Add(pid);

        if (GetProcessSampleException is { } exception) {
            throw exception;
        }

        return samples.SingleOrDefault(sample => sample.Pid == pid);
    }

    // When set, GetProcessSample throws it, as a failed lookup would.
    public Exception? GetProcessSampleException { get; set; }

    public List<ThreadInfo> GetThreads(int pid)
    {
        ThreadRequestedPids.Add(pid);
        return threadInfos;
    }

    public List<int> ModuleRequestedPids { get; } = [];

    public List<int> SampleRequestedPids { get; } = [];

    public List<int> ThreadRequestedPids { get; } = [];
}
