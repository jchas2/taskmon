using Task.Monitor.System;

namespace Task.Monitor.Process;

public class ProcessorEventArgs(List<ProcessorInfo> processInfos, SystemStatistics systemStatistics)
    : EventArgs
{
    public readonly List<ProcessorInfo> ProcessInfos = processInfos;
    public readonly SystemStatistics SystemStatistics = systemStatistics;
}
