using Task.Monitor.System.Services.Process;

namespace Task.Monitor.Tests.Process;

// Stands in for ProcessService's single-process lookups once registered in the ServiceController. It is
// never started, so it samples nothing, and it ends no real processes.
public sealed class ProcessServiceFake : ProcessService
{
    private readonly List<ModuleInfo>    moduleInfos = [];
    private readonly List<ProcessSample> samples     = [];
    private readonly List<ThreadInfo>    threadInfos = [];

    public ProcessServiceFake Add(ModuleInfo moduleInfo)
    {
        moduleInfos.Add(moduleInfo);
        return this;
    }

    public ProcessServiceFake Add(ProcessSample sample)
    {
        samples.Add(sample);
        return this;
    }

    public ProcessServiceFake Add(ThreadInfo threadInfo)
    {
        threadInfos.Add(threadInfo);
        return this;
    }

    public List<int> EndedPids { get; } = [];

    public override bool EndTask(int pid, int timeoutMilliseconds)
    {
        EndedPids.Add(pid);
        return true;
    }

    public override List<ModuleInfo> GetModules(int pid)
    {
        ModuleRequestedPids.Add(pid);
        return moduleInfos;
    }

    public override ProcessSample? GetProcessSample(int pid)
    {
        SampleRequestedPids.Add(pid);

        if (GetProcessSampleException is { } exception) {
            throw exception;
        }

        return samples.SingleOrDefault(sample => sample.Pid == pid);
    }

    // When set, GetProcessSample throws it, as a failed lookup would.
    public Exception? GetProcessSampleException { get; set; }

    public override List<ThreadInfo> GetThreads(int pid)
    {
        ThreadRequestedPids.Add(pid);
        return threadInfos;
    }

    public List<int> ModuleRequestedPids { get; } = [];

    public List<int> SampleRequestedPids { get; } = [];

    public List<int> ThreadRequestedPids { get; } = [];
}
