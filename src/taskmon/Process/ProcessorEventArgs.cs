using Task.Monitor.System;

namespace Task.Monitor.Process;

public class ProcessorEventArgs(
    List<ProcessorInfo> processInfos,
    SystemStatistics statistics)
    : EventArgs
{
    public readonly List<ProcessorInfo> ProcessInfos = processInfos;
    public readonly SystemStatistics Statistics = statistics;
}
