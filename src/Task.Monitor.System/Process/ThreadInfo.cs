namespace Task.Monitor.System.Process;

public class ThreadInfo
{
    public int ThreadId { get; set; } = 0;
    public string ThreadState { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int Priority { get; set; } = 0;
    public long StartAddress { get; set; } = nint.Zero;
    public TimeSpan CpuKernelTime { get; set; } = TimeSpan.Zero;
    public TimeSpan CpuUserTime { get; set; } = TimeSpan.Zero;
    public TimeSpan CpuTotalTime { get; set; } = TimeSpan.Zero;
}
