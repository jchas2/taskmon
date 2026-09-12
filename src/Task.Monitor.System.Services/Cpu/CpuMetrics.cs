namespace Task.Monitor.System.Services.Cpu;

public sealed class CpuMetrics
{
    public double CpuPercentIdleTime      { get; set; }
    public double CpuPercentKernelTime    { get; set; }
    public double CpuPercentUserTime      { get; set; }
    public double CpuTotalTime            { get; set; } 
}
