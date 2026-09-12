namespace Task.Monitor.System.Services.Process;

public sealed class ProcessEntryAverage
{
    private long count;
    private double cpuTimePercentMean;
    private double gpuTimePercentMean;
    private double usedMemoryMean;
    private double diskBytesPerSecondMean;

    public void Add(ProcessEntry entry)
    {
        // Use Welford's incremental mean for numerical stability over long runs.
        count++;

        cpuTimePercentMean += (entry.CpuTimePercent - cpuTimePercentMean) / count;
        gpuTimePercentMean += (entry.GpuTimePercent - gpuTimePercentMean) / count;
        usedMemoryMean += ((double)entry.UsedMemory - usedMemoryMean) / count;
        diskBytesPerSecondMean += (entry.DiskBytesPerSecond - diskBytesPerSecondMean) / count;

        CpuTimePercentMax = Math.Max(CpuTimePercentMax, entry.CpuTimePercent);
        GpuTimePercentMax = Math.Max(GpuTimePercentMax, entry.GpuTimePercent);
        UsedMemoryMax = Math.Max(UsedMemoryMax, entry.UsedMemory);
        DiskBytesPerSecondMax = Math.Max(DiskBytesPerSecondMax, entry.DiskBytesPerSecond);
    }

    public double CpuTimePercent => cpuTimePercentMean;
    public double GpuTimePercent => gpuTimePercentMean;
    public long UsedMemory => (long)usedMemoryMean;
    public double DiskBytesPerSecond => diskBytesPerSecondMean;

    public double CpuTimePercentMax { get; private set; }
    public double GpuTimePercentMax { get; private set; }
    public long UsedMemoryMax { get; private set; }
    public double DiskBytesPerSecondMax { get; private set; }
}
