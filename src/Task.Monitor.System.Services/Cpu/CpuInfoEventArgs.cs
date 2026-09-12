namespace Task.Monitor.System.Services.Cpu;

public sealed class CpuInfoEventArgs(CpuInfo cpuInfo) : EventArgs
{
    public CpuInfo? Info { get; } = cpuInfo;
}
