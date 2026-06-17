namespace Task.Monitor.Process;

internal sealed class ProcessAverage
{
    private long count;
    private double cpuTimePercentMean;
    private double gpuTimePercentMean;
    private double usedMemoryMean;
    private double diskUsageMean;

    public void Add(ProcessorInfo processorInfo)
    {
        // Use Welford's incremental mean for numerical stability over long runs.
        count++;
        cpuTimePercentMean += (processorInfo.CpuTimePercent - cpuTimePercentMean) / count;
        gpuTimePercentMean += (processorInfo.GpuTimePercent - gpuTimePercentMean) / count;
        usedMemoryMean += ((double)processorInfo.UsedMemory - usedMemoryMean) / count;
        diskUsageMean += ((double)processorInfo.DiskUsage - diskUsageMean) / count;
    }

    public double CpuTimePercent => cpuTimePercentMean;
    public double GpuTimePercent => gpuTimePercentMean;
    public long UsedMemory => (long)usedMemoryMean;
    public long DiskUsage => (long)diskUsageMean;
}
