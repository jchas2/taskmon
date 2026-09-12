namespace Task.Monitor.System.Services.Cpu;

public sealed class CpuSystemTimes
{
    public long Idle   { get; set; }
    public long Kernel { get; set; }
    public long User   { get; set; }
}
