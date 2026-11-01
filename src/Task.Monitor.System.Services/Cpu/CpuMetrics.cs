namespace Task.Monitor.System.Services.Cpu;

public sealed class CpuMetrics
{
    public double CpuPercentIdleTime      { get; set; }
    public double CpuPercentKernelTime    { get; set; }
    public double CpuPercentUserTime      { get; set; }
    public double CpuTotalTime            { get; set; }

    // Null where the platform doesn't report it.
    public double? DriverTimePercent       { get; set; }
    public double? PerformanceLimitPercent { get; set; }
    public double? RunQueuePerCore         { get; set; }
}
